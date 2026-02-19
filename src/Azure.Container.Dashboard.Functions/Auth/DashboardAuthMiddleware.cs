using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Azure.Container.Dashboard.Auth;

internal sealed class DashboardAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DashboardOptions _options;

    public DashboardAuthMiddleware(RequestDelegate next, DashboardOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var prefix = "/" + _options.RoutePrefix.TrimStart('/');

        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!_options.RequireAuthentication)
        {
            await _next(context);
            return;
        }

        var principal = ClientPrincipalParser.Parse(context.Request);

        if (principal is null)
        {
            await WriteJsonError(context, 401,
                "Authentication required. Please configure Easy Auth on your Container App.");
            return;
        }

        if (_options.AllowedRoles.Count > 0)
        {
            var userRoles = ClientPrincipalParser.GetUserRoles(principal);
            var hasRole = _options.AllowedRoles.Any(r =>
                userRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

            if (!hasRole)
            {
                await WriteJsonError(context, 403,
                    "Access denied. You do not have the required role to access this dashboard.");
                return;
            }
        }

        await _next(context);
    }

    private static async Task WriteJsonError(HttpContext context, int statusCode, string error)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { error }));
    }
}
