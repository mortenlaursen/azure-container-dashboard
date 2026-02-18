using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureContainerFunctions;

public class AppInsightsQueryResponse
{
    [JsonPropertyName("tables")]
    public List<AppInsightsTable> Tables { get; set; } = [];
}

public class AppInsightsTable
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("columns")]
    public List<AppInsightsColumn> Columns { get; set; } = [];

    [JsonPropertyName("rows")]
    public List<List<JsonElement>> Rows { get; set; } = [];
}

public class AppInsightsColumn
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public class FunctionInvocation
{
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public string ResultCode { get; set; } = "";
    public double DurationMs { get; set; }
    public string InvocationId { get; set; } = "";
    public string OperationId { get; set; } = "";
    public string OperationName { get; set; } = "";
}

public class InvocationTrace
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = "";
    public int SeverityLevel { get; set; }

    public string SeverityLabel => SeverityLevel switch
    {
        0 => "Verbose",
        1 => "Information",
        2 => "Warning",
        3 => "Error",
        4 => "Critical",
        _ => "Unknown"
    };
}
