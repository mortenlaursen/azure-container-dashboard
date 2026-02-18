using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Azure.Container.Dashboard.Functions;

public class AppManagementFunctions
{
    private readonly ContainerAppFunctionsClient _client;
    private readonly DashboardOptions _options;

    public AppManagementFunctions(ContainerAppFunctionsClient client, DashboardOptions options)
    {
        _client = client;
        _options = options;
    }

    [Function("DashboardAppStatus")]
    public async Task<IActionResult> GetStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard/status")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (!ValidateOptions(out var error))
            return new BadRequestObjectResult(new { error });

        try
        {
            var app = await _client.GetContainerAppAsync(
                _options.SubscriptionId!, _options.ResourceGroup!, _options.AppName!, cancellationToken);

            return new OkObjectResult(new
            {
                appName = _options.AppName,
                runningStatus = app.Properties?.RunningStatus,
                latestRevision = app.Properties?.LatestRevisionName
            });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    [Function("DashboardAppStart")]
    public async Task<IActionResult> StartApp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "dashboard/app/start")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (!ValidateOptions(out var error))
            return new BadRequestObjectResult(new { error });

        try
        {
            await _client.StartContainerAppAsync(
                _options.SubscriptionId!, _options.ResourceGroup!, _options.AppName!, cancellationToken);

            return new OkObjectResult(new { message = $"Container App '{_options.AppName}' is starting." });
        }
        catch (Exception ex)
        {
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    [Function("DashboardAppStop")]
    public async Task<IActionResult> StopApp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "dashboard/app/stop")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (!ValidateOptions(out var error))
            return new BadRequestObjectResult(new { error });

        try
        {
            await _client.StopContainerAppAsync(
                _options.SubscriptionId!, _options.ResourceGroup!, _options.AppName!, cancellationToken);

            return new OkObjectResult(new { message = $"Container App '{_options.AppName}' is stopping." });
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
