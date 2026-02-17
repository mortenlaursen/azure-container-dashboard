using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;

namespace AzureContainerFunctions;

public class ContainerAppFunctionsClient
{
    private const string ApiVersion = "2025-10-02-preview";
    private const string ManagementEndpoint = "https://management.azure.com";

    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;

    public ContainerAppFunctionsClient(TokenCredential? credential = null)
    {
        _credential = credential ?? new AzureCliCredential();
        _httpClient = new HttpClient();
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var context = new TokenRequestContext(["https://management.azure.com/.default"]);
        var token = await _credential.GetTokenAsync(context, cancellationToken);
        return token.Token;
    }

    /// <summary>
    /// Lists all functions in the latest revision of a Container App.
    /// Equivalent to: az containerapp function list -g {resourceGroup} -n {appName}
    /// </summary>
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

    /// <summary>
    /// Gets a specific function by name from the latest revision.
    /// Equivalent to: az containerapp function show -g {resourceGroup} -n {appName} --function-name {functionName}
    /// </summary>
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

    /// <summary>
    /// Lists all functions in a specific revision of a Container App.
    /// Equivalent to: az containerapp function list -g {resourceGroup} -n {appName} --revision {revisionName}
    /// </summary>
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

    /// <summary>
    /// Gets the Container App resource (needed to read current template before PATCH).
    /// </summary>
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

    /// <summary>
    /// Enables or disables a function by setting the AzureWebJobs.{functionName}.Disabled
    /// environment variable on the Container App.
    /// </summary>
    public async Task SetFunctionDisabledAsync(
        string subscriptionId,
        string resourceGroupName,
        string containerAppName,
        string functionName,
        bool disabled,
        CancellationToken cancellationToken = default)
    {
        var containerApp = await GetContainerAppAsync(subscriptionId, resourceGroupName, containerAppName, cancellationToken);

        var containers = containerApp.Properties?.Template?.Containers;
        if (containers == null || containers.Count == 0)
            throw new InvalidOperationException("Container App has no containers defined.");

        var container = containers[0];
        container.Env ??= [];

        var envVarName = $"AzureWebJobs.{functionName}.Disabled";
        var existingVar = container.Env.FirstOrDefault(e => e.Name == envVarName);

        if (existingVar != null)
        {
            existingVar.Value = disabled ? "true" : "false";
        }
        else
        {
            container.Env.Add(new EnvironmentVariable { Name = envVarName, Value = disabled ? "true" : "false" });
        }

        var url = $"{ManagementEndpoint}/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}" +
                  $"/providers/Microsoft.App/containerApps/{containerAppName}?api-version={ApiVersion}";

        await SendPatchRequestAsync<ContainerApp>(url, containerApp, cancellationToken);
    }

    /// <summary>
    /// Stops the entire Container App.
    /// POST .../containerApps/{name}/stop
    /// </summary>
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

    /// <summary>
    /// Starts the entire Container App.
    /// POST .../containerApps/{name}/start
    /// </summary>
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

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Azure ARM request failed with {response.StatusCode}: {content}");
        }
    }

    private async Task<T> SendRequestAsync<T>(string url, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azure ARM request failed with {response.StatusCode}: {content}");
        }

        return JsonSerializer.Deserialize<T>(content)
               ?? throw new JsonException($"Failed to deserialize response from {url}");
    }

    private static readonly JsonSerializerOptions PatchSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private async Task<T> SendPatchRequestAsync<T>(string url, object body, CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);

        var json = JsonSerializer.Serialize(body, PatchSerializerOptions);

        using var request = new HttpRequestMessage(HttpMethod.Patch, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/merge-patch+json");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azure ARM request failed with {response.StatusCode}: {content}");
        }

        return JsonSerializer.Deserialize<T>(content)
               ?? throw new JsonException($"Failed to deserialize response from {url}");
    }
}
