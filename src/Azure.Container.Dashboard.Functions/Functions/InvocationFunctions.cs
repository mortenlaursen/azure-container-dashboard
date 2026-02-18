using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Azure.Container.Dashboard.Functions;

public class InvocationFunctions
{
    private readonly ContainerAppFunctionsClient _armClient;
    private readonly AppInsightsClient _insightsClient;
    private readonly DashboardOptions _options;

    public InvocationFunctions(
        ContainerAppFunctionsClient armClient,
        AppInsightsClient insightsClient,
        DashboardOptions options)
    {
        _armClient = armClient;
        _insightsClient = insightsClient;
        _options = options;
    }

    [Function("DashboardInvocations")]
    public async Task<IActionResult> GetInvocations(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/invocations/{name}")] HttpRequest req,
        string name,
        CancellationToken cancellationToken)
    {
        if (!ValidateOptions(out var error))
            return new BadRequestObjectResult(new { error });

        try
        {
            var appId = await _armClient.GetAppInsightsAppIdAsync(
                _options.SubscriptionId!, _options.ResourceGroup!, _options.AppName!, cancellationToken);

            if (string.IsNullOrEmpty(appId))
                return new BadRequestObjectResult(new { error = "Application Insights is not configured for this container app." });

            var timespan = req.Query.ContainsKey("timespan") ? req.Query["timespan"].ToString() : "P1D";
            var limit = int.TryParse(req.Query["limit"], out var l) && l > 0 && l <= 1000 ? l : 50;
            var invocations = await _insightsClient.GetInvocationsAsync(appId, name, timespan: timespan, limit: limit, cancellationToken: cancellationToken);

            var result = invocations.Select(i => new
            {
                timestamp = i.Timestamp,
                success = i.Success,
                resultCode = i.ResultCode,
                durationMs = i.DurationMs,
                invocationId = i.InvocationId,
                operationId = i.OperationId
            });

            return new OkObjectResult(result);
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    [Function("DashboardInvocationCounts")]
    public async Task<IActionResult> GetInvocationCounts(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/invocations/{name}/counts")] HttpRequest req,
        string name,
        CancellationToken cancellationToken)
    {
        if (!ValidateOptions(out var error))
            return new BadRequestObjectResult(new { error });

        try
        {
            var appId = await _armClient.GetAppInsightsAppIdAsync(
                _options.SubscriptionId!, _options.ResourceGroup!, _options.AppName!, cancellationToken);

            if (string.IsNullOrEmpty(appId))
                return new BadRequestObjectResult(new { error = "Application Insights is not configured for this container app." });

            var timespan = req.Query.ContainsKey("timespan") ? req.Query["timespan"].ToString() : "P30D";
            var counts = await _insightsClient.GetInvocationCountsAsync(appId, name, timespan: timespan, cancellationToken: cancellationToken);

            return new OkObjectResult(new
            {
                total = counts.Total,
                failed = counts.Failed,
                succeeded = counts.Succeeded
            });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    [Function("DashboardTraces")]
    public async Task<IActionResult> GetTraces(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/traces/{operationId}")] HttpRequest req,
        string operationId,
        CancellationToken cancellationToken)
    {
        if (!ValidateOptions(out var error))
            return new BadRequestObjectResult(new { error });

        try
        {
            var appId = await _armClient.GetAppInsightsAppIdAsync(
                _options.SubscriptionId!, _options.ResourceGroup!, _options.AppName!, cancellationToken);

            if (string.IsNullOrEmpty(appId))
                return new BadRequestObjectResult(new { error = "Application Insights is not configured for this container app." });

            var traces = await _insightsClient.GetInvocationTracesAsync(appId, operationId, cancellationToken);

            var result = traces.Select(t => new
            {
                timestamp = t.Timestamp,
                message = t.Message,
                severityLevel = t.SeverityLevel,
                severityLabel = t.SeverityLabel
            });

            return new OkObjectResult(result);
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
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
