using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace Azure.Container.Dashboard.Functions;

public class DashboardUiFunction
{
    [Function("DashboardUI")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "dashboard")] HttpRequest req)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Azure.Container.Dashboard.wwwroot.dashboard.html");

        if (stream is null)
            return new NotFoundObjectResult("Dashboard HTML not found.");

        using var reader = new StreamReader(stream);
        var html = reader.ReadToEnd();

        return new ContentResult
        {
            Content = html,
            ContentType = "text/html",
            StatusCode = 200
        };
    }
}
