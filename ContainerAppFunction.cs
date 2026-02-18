using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureContainerFunctions;

public class ContainerAppFunctionCollection
{
    [JsonPropertyName("value")]
    public List<ContainerAppFunction> Value { get; set; } = [];

    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }
}

public class ContainerAppFunction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string RawName { get; set; } = "";

    /// <summary>
    /// Returns the function name, falling back to extracting it from the resource ID
    /// if the API doesn't populate the name field (known MS bug).
    /// </summary>
    [JsonIgnore]
    public string Name => !string.IsNullOrEmpty(RawName)
        ? RawName
        : Id.Split('/')[^1];

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("properties")]
    public ContainerAppFunctionProperties Properties { get; set; } = new();
}

public class ContainerAppFunctionProperties
{
    [JsonPropertyName("invokeUrlTemplate")]
    public string? InvokeUrlTemplate { get; set; }

    [JsonPropertyName("triggerType")]
    public string? TriggerType { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("isDisabled")]
    public bool IsDisabled { get; set; }
}

public class ContainerApp
{
    [JsonPropertyName("location")]
    public string Location { get; set; } = "";

    [JsonPropertyName("properties")]
    public ContainerAppProperties? Properties { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class ContainerAppProperties
{
    [JsonPropertyName("provisioningState")]
    public string? ProvisioningState { get; set; }

    [JsonPropertyName("runningStatus")]
    public string? RunningStatus { get; set; }

    [JsonPropertyName("latestRevisionName")]
    public string? LatestRevisionName { get; set; }

    [JsonPropertyName("template")]
    public ContainerAppTemplate? Template { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class ContainerAppTemplate
{
    [JsonPropertyName("containers")]
    public List<ContainerAppContainer> Containers { get; set; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class ContainerAppContainer
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("image")]
    public string Image { get; set; } = "";

    [JsonPropertyName("env")]
    public List<EnvironmentVariable>? Env { get; set; }

    [JsonPropertyName("resources")]
    public JsonElement? Resources { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

public class EnvironmentVariable
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("secretRef")]
    public string? SecretRef { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
