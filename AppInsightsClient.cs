using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;

namespace Azure.Container.Dashboard;

public class AppInsightsClient
{
    private const string AppInsightsEndpoint = "https://api.applicationinsights.io/v1/apps";

    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;

    public AppInsightsClient(TokenCredential? credential = null)
    {
        _credential = credential ?? new AzureCliCredential();
        _httpClient = new HttpClient();
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var context = new TokenRequestContext(["https://api.applicationinsights.io/.default"]);
        var token = await _credential.GetTokenAsync(context, cancellationToken);
        return token.Token;
    }

    public async Task<List<FunctionInvocation>> GetInvocationsAsync(
        string appId,
        string functionName,
        string timespan = "P1D",
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var kql = $@"requests
| extend functionNameFromCustomDimension = tostring(customDimensions['faas.name']),
         invocationId = coalesce(tostring(customDimensions['InvocationId']), tostring(customDimensions['faas.invocation_id']))
| where timestamp > ago(24h)
| where operation_Name =~ '{functionName}' or functionNameFromCustomDimension =~ '{functionName}'
| order by timestamp desc
| take {limit}
| project timestamp, success, resultCode, durationInMilliSeconds=duration, invocationId, operationId=operation_Id, operationName=operation_Name";

        var response = await QueryAsync(appId, kql, timespan, cancellationToken);
        return ParseInvocations(response);
    }

    public async Task<List<InvocationTrace>> GetInvocationTracesAsync(
        string appId,
        string operationId,
        CancellationToken cancellationToken = default)
    {
        var kql = $@"traces
| where operation_Id == '{operationId}'
| order by timestamp asc
| project timestamp, message, severityLevel";

        var response = await QueryAsync(appId, kql, "P1D", cancellationToken);
        return ParseTraces(response);
    }

    private async Task<AppInsightsQueryResponse> QueryAsync(
        string appId,
        string kql,
        string timespan,
        CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var url = $"{AppInsightsEndpoint}/{appId}/query";

        var body = JsonSerializer.Serialize(new { query = kql, timespan });

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Application Insights query failed with {response.StatusCode}: {content}");
        }

        return JsonSerializer.Deserialize<AppInsightsQueryResponse>(content)
               ?? throw new JsonException("Failed to deserialize Application Insights response");
    }

    private static List<FunctionInvocation> ParseInvocations(AppInsightsQueryResponse response)
    {
        var results = new List<FunctionInvocation>();
        if (response.Tables.Count == 0) return results;

        var table = response.Tables[0];
        var colIndex = BuildColumnIndex(table.Columns);

        foreach (var row in table.Rows)
        {
            results.Add(new FunctionInvocation
            {
                Timestamp = GetDateTime(row, colIndex, "timestamp"),
                Success = GetBool(row, colIndex, "success"),
                ResultCode = GetString(row, colIndex, "resultCode"),
                DurationMs = GetDouble(row, colIndex, "durationInMilliSeconds"),
                InvocationId = GetString(row, colIndex, "invocationId"),
                OperationId = GetString(row, colIndex, "operationId"),
                OperationName = GetString(row, colIndex, "operationName")
            });
        }

        return results;
    }

    private static List<InvocationTrace> ParseTraces(AppInsightsQueryResponse response)
    {
        var results = new List<InvocationTrace>();
        if (response.Tables.Count == 0) return results;

        var table = response.Tables[0];
        var colIndex = BuildColumnIndex(table.Columns);

        foreach (var row in table.Rows)
        {
            results.Add(new InvocationTrace
            {
                Timestamp = GetDateTime(row, colIndex, "timestamp"),
                Message = GetString(row, colIndex, "message"),
                SeverityLevel = GetInt(row, colIndex, "severityLevel")
            });
        }

        return results;
    }

    private static Dictionary<string, int> BuildColumnIndex(List<AppInsightsColumn> columns)
    {
        var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columns.Count; i++)
            index[columns[i].Name] = i;
        return index;
    }

    private static string GetString(List<JsonElement> row, Dictionary<string, int> colIndex, string name)
    {
        if (!colIndex.TryGetValue(name, out var idx) || idx >= row.Count) return "";
        var el = row[idx];
        return el.ValueKind == JsonValueKind.Null ? "" : el.ToString();
    }

    private static bool GetBool(List<JsonElement> row, Dictionary<string, int> colIndex, string name)
    {
        if (!colIndex.TryGetValue(name, out var idx) || idx >= row.Count) return false;
        var el = row[idx];
        if (el.ValueKind == JsonValueKind.True) return true;
        if (el.ValueKind == JsonValueKind.False) return false;
        if (el.ValueKind == JsonValueKind.String) return bool.TryParse(el.GetString(), out var b) && b;
        return false;
    }

    private static double GetDouble(List<JsonElement> row, Dictionary<string, int> colIndex, string name)
    {
        if (!colIndex.TryGetValue(name, out var idx) || idx >= row.Count) return 0;
        var el = row[idx];
        if (el.ValueKind == JsonValueKind.Number) return el.GetDouble();
        if (el.ValueKind == JsonValueKind.String && double.TryParse(el.GetString(), out var d)) return d;
        return 0;
    }

    private static int GetInt(List<JsonElement> row, Dictionary<string, int> colIndex, string name)
    {
        if (!colIndex.TryGetValue(name, out var idx) || idx >= row.Count) return 0;
        var el = row[idx];
        if (el.ValueKind == JsonValueKind.Number) return el.GetInt32();
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var i)) return i;
        return 0;
    }

    private static DateTime GetDateTime(List<JsonElement> row, Dictionary<string, int> colIndex, string name)
    {
        if (!colIndex.TryGetValue(name, out var idx) || idx >= row.Count) return default;
        var el = row[idx];
        if (el.ValueKind == JsonValueKind.String && DateTime.TryParse(el.GetString(), out var dt)) return dt;
        return default;
    }
}
