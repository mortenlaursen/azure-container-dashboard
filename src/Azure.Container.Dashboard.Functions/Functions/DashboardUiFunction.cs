using System.Reflection;
using System.Text.Encodings.Web;
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

        // Inject server-side user info from Easy Auth header if present
        if (req.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL-NAME", out var principalName)
            && !string.IsNullOrWhiteSpace(principalName))
        {
            var encodedName = JavaScriptEncoder.Default.Encode(principalName.ToString());
            var script = $"<script>window.__DASHBOARD_USER__ = {{ name: \"{encodedName}\" }};</script>";
            html = html.Replace("</head>", script + "</head>");
        }

        return new ContentResult
        {
            Content = html,
            ContentType = "text/html",
            StatusCode = 200
        };
    }
}
