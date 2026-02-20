using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Azure.Container.Dashboard.Functions;

public class FunctionManagementFunctions
{
    private static readonly HashSet<string> DashboardFunctionNames =
    [
        "DashboardUI",
        "DashboardFunctionsList",
        "DashboardFunctionsUpdate",
        "DashboardInvocations",
        "DashboardInvocationCounts",
        "DashboardTraces",
        "DashboardAppStatus",
        "DashboardAppStart",
        "DashboardAppStop",
    ];

    private readonly ContainerAppFunctionsClient _client;
    private readonly DashboardOptions _options;

    public FunctionManagementFunctions(ContainerAppFunctionsClient client, DashboardOptions options)
    {
        _client = client;
        _options = options;
    }

    [Function("DashboardFunctionsList")]
    public async Task<IActionResult> ListFunctions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/functions")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (!ValidateOptions(out var error))
            return new BadRequestObjectResult(new { error });

        try
        {
            var result = await _client.ListFunctionsAsync(
                _options.SubscriptionId!, _options.ResourceGroup!, _options.AppName!, cancellationToken);

            var functions = result.Value
                .Where(f => !DashboardFunctionNames.Contains(f.Name))
                .Select(f => new
                {
                    name = f.Name,
                    triggerType = f.Properties.TriggerType,
                    language = f.Properties.Language,
                    isDisabled = f.Properties.IsDisabled,
                    invokeUrlTemplate = f.Properties.InvokeUrlTemplate
                });

            return new OkObjectResult(functions);
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    [Function("DashboardFunctionsUpdate")]
    public async Task<IActionResult> UpdateFunctions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "dashboard/functions/update")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (!ValidateOptions(out var error))
            return new BadRequestObjectResult(new { error });

        try
        {
            var body = await JsonSerializer.DeserializeAsync<FunctionsUpdateRequest>(req.Body, cancellationToken: cancellationToken);
            var toDisable = body?.Disable ?? [];
            var toEnable = body?.Enable ?? [];

            if (toDisable.Length == 0 && toEnable.Length == 0)
                return new BadRequestObjectResult(new { error = "Request body must include 'disable' and/or 'enable' arrays." });

            await _client.UpdateFunctionsStateAsync(
                _options.SubscriptionId!, _options.ResourceGroup!, _options.AppName!, toDisable, toEnable, cancellationToken);

            var total = toDisable.Length + toEnable.Length;
            return new OkObjectResult(new { message = $"{total} function(s) updated. A new revision is being created." });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    private class FunctionsUpdateRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("disable")]
        public string[] Disable { get; set; } = [];

        [System.Text.Json.Serialization.JsonPropertyName("enable")]
        public string[] Enable { get; set; } = [];
    }

    private bool ValidateOptions(out string error)
    {
        if (string.IsNullOrWhiteSpace(_options.SubscriptionId) ||
            string.IsNullOrWhiteSpace(_options.ResourceGroup) ||
            string.IsNullOrWhiteSpace(_options.AppName))
        {
            error = "Dashboard is not configured. Set AZURE_SUBSCRIPTION_ID, AZURE_RESOURCE_GROUP, and CONTAINER_APP_NAME environment variables.";
            return false;
        }
        error = "";
        return true;
    }
}
