using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.Core;

namespace Azure.Container.Dashboard;

public class ContainerAppFunctionsClient
{
    private const string ApiVersion = "2025-10-02-preview";
    private const string ManagementEndpoint = "https://management.azure.com";

    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;

    public ContainerAppFunctionsClient(HttpClient httpClient, TokenCredential credential)
    {
        _httpClient = httpClient;
        _credential = credential;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var context = new TokenRequestContext(["https://management.azure.com/.default"]);
        var token = await _credential.GetTokenAsync(context, cancellationToken);
        return token.Token;
    }

    public async Task<ContainerAppFunctionCollection> ListFunctionsAsync(
        string subscriptionId,
        string resourceGroupName,
        string containerAppName,
        CancellationToken cancellationToken = default)
    {
        var url = $"{ManagementEndpoint}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}" +
                  $"/providers/Microsoft.App/containerApps/{containerAppName}/functions?api-version={ApiVersion}";

        return await SendRequestAsync<ContainerAppFunctionCollection>(url, cancellationToken);
    }

    public async Task<ContainerAppFunction> GetFunctionAsync(
        string subscriptionId,
        string resourceGroupName,
        string containerAppName,
        string functionName,
        CancellationToken cancellationToken = default)
    {
        var url = $"{ManagementEndpoint}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}" +
                  $"/providers/Microsoft.App/containerApps/{containerAppName}/functions/{functionName}?api-version={ApiVersion}";

        return await SendRequestAsync<ContainerAppFunction>(url, cancellationToken);
    }

    public async Task<ContainerAppFunctionCollection> ListFunctionsByRevisionAsync(
        string subscriptionId,
        string resourceGroupName,
        string containerAppName,
        string revisionName,
        CancellationToken cancellationToken = default)
    {
        var url = $"{ManagementEndpoint}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}" +
                  $"/providers/Microsoft.App/containerApps/{containerAppName}/revisions/{revisionName}/functions?api-version={ApiVersion}";

        return await SendRequestAsync<ContainerAppFunctionCollection>(url, cancellationToken);
    }

    public async Task<ContainerApp> GetContainerAppAsync(
        string subscriptionId,
        string resourceGroupName,
        string containerAppName,
        CancellationToken cancellationToken = default)
    {
        var url = $"{ManagementEndpoint}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}" +
                  $"/providers/Microsoft.App/containerApps/{containerAppName}?api-version={ApiVersion}";

        return await SendRequestAsync<ContainerApp>(url, cancellationToken);
    }

    public async Task UpdateFunctionsStateAsync(
        string subscriptionId,
        string resourceGroupName,
        string containerAppName,
        IReadOnlyList<string> functionsToDisable,
        IReadOnlyList<string> functionsToEnable,
        CancellationToken cancellationToken = default)
    {
        const int maxRetries = 10;
        const int pollDelayMs = 3000;

        var url = $"{ManagementEndpoint}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}" +
                  $"/providers/Microsoft.App/containerApps/{containerAppName}?api-version={ApiVersion}";

        for (var attempt = 0; ; attempt++)
        {
            var rawJson = await GetRawJsonAsync(url, cancellationToken);
            var doc = JsonNode.Parse(rawJson)
                      ?? throw new JsonException("Failed to parse container app JSON");

            // Wait for any in-progress provisioning before attempting PATCH
            var provisioningState = doc["properties"]?["provisioningState"]?.GetValue<string>();
            if (provisioningState != null &&
                !string.Equals(provisioningState, "Succeeded", StringComparison.OrdinalIgnoreCase))
            {
                if (attempt >= maxRetries)
                    throw new InvalidOperationException(
                        $"Container App is still provisioning (state: {provisioningState}) after {maxRetries} retries.");

                await Task.Delay(pollDelayMs, cancellationToken);
                continue;
            }

            var containers = doc["properties"]?["template"]?["containers"]?.AsArray();
            if (containers == null || containers.Count == 0)
                throw new InvalidOperationException("Container App has no containers defined.");

            var container = containers[0]!.AsObject();
            var env = container["env"]?.AsArray();
            if (env == null)
            {
                env = new JsonArray();
                container["env"] = env;
            }

            // Disable: add or update env var to "true"
            foreach (var functionName in functionsToDisable)
            {
                var envVarName = $"AzureWebJobs_{functionName}_Disabled";
                var existing = env.FirstOrDefault(e => e?["name"]?.GetValue<string>() == envVarName);

                if (existing != null)
                {
                    existing.AsObject()["value"] = "true";
                }
                else
                {
                    env.Add(new JsonObject
                    {
                        ["name"] = envVarName,
                        ["value"] = "true"
                    });
                }
            }

            // Enable: remove the env var entirely
            foreach (var functionName in functionsToEnable)
            {
                var envVarName = $"AzureWebJobs_{functionName}_Disabled";
                var existing = env.FirstOrDefault(e => e?["name"]?.GetValue<string>() == envVarName);
                if (existing != null)
                    env.Remove(existing);
            }

            // Build revision suffix based on the current revision name
            var template = doc["properties"]!["template"]!.AsObject();
            var latestRevisionName = doc["properties"]?["latestRevisionName"]?.GetValue<string>();
            template["revisionSuffix"] = BuildRevisionSuffix(latestRevisionName, containerAppName);

            var patchBody = new JsonObject
            {
                ["properties"] = new JsonObject
                {
                    ["template"] = template.DeepClone()
                }
            };

            try
            {
                await SendPatchAsync(url, patchBody.ToJsonString(), cancellationToken);
                return;
            }
            catch (HttpRequestException ex) when (
                attempt < maxRetries &&
                ex.Message.Contains("ContainerAppOperationInProgress", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(pollDelayMs, cancellationToken);
            }
        }
    }

    internal static string BuildRevisionSuffix(string? latestRevisionName, string containerAppName)
    {
        if (string.IsNullOrEmpty(latestRevisionName))
            return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

        var prefix = $"{containerAppName}--";
        var currentSuffix = latestRevisionName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? latestRevisionName[prefix.Length..]
            : latestRevisionName;

        var lastDash = currentSuffix.LastIndexOf('-');
        if (lastDash > 0 && int.TryParse(currentSuffix.AsSpan(lastDash + 1), out var counter))
            return $"{currentSuffix[..lastDash]}-{counter + 1}";

        return $"{currentSuffix}-1";
    }

    public async Task StopContainerAppAsync(
        string subscriptionId,
        string resourceGroupName,
        string containerAppName,
        CancellationToken cancellationToken = default)
    {
        var url = $"{ManagementEndpoint}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}" +
                  $"/providers/Microsoft.App/containerApps/{containerAppName}/stop?api-version={ApiVersion}";

        await SendPostAsync(url, cancellationToken);
    }

    public async Task StartContainerAppAsync(
        string subscriptionId,
        string resourceGroupName,
        string containerAppName,
        CancellationToken cancellationToken = default)
    {
        var url = $"{ManagementEndpoint}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}" +
                  $"/providers/Microsoft.App/containerApps/{containerAppName}/start?api-version={ApiVersion}";

        await SendPostAsync(url, cancellationToken);
    }

    private async Task SendPostAsync(string url, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Azure ARM request failed with {response.StatusCode}: {content}");
        }
    }

    public async Task<string?> GetAppInsightsAppIdAsync(
        string subscriptionId,
        string resourceGroupName,
        string containerAppName,
        CancellationToken cancellationToken = default)
    {
        var containerApp = await GetContainerAppAsync(subscriptionId, resourceGroupName, containerAppName, cancellationToken);

        var containers = containerApp.Properties?.Template?.Containers;
        if (containers == null || containers.Count == 0)
            return null;

        var connectionString = containers
            .SelectMany(c => c.Env ?? [])
            .FirstOrDefault(e => e.Name == "APPLICATIONINSIGHTS_CONNECTION_STRING")
            ?.Value;

        if (string.IsNullOrEmpty(connectionString))
            return null;

        return ParseAppIdFromConnectionString(connectionString);
    }

    internal static string? ParseAppIdFromConnectionString(string connectionString)
    {
        foreach (var part in connectionString.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("ApplicationId=", StringComparison.OrdinalIgnoreCase))
                return trimmed["ApplicationId=".Length..];
        }
        return null;
    }

    private async Task<T> SendRequestAsync<T>(string url, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azure ARM request failed with {response.StatusCode}: {content}");
        }

        return JsonSerializer.Deserialize<T>(content)
               ?? throw new JsonException($"Failed to deserialize response from {url}");
    }

    private async Task<string> GetRawJsonAsync(string url, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Azure ARM request failed with {response.StatusCode}: {content}");

        return content;
    }

    private async Task SendPatchAsync(string url, string jsonBody, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Azure ARM request failed with {response.StatusCode}: {content}");
        }
    }
}
