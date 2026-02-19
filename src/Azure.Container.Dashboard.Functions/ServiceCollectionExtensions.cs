using System.Text.Json;
using Azure.Container.Dashboard.Auth;
using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Azure.Container.Dashboard;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContainerDashboard(
        this IServiceCollection services,
        Action<DashboardOptions>? configure = null)
    {
        var options = new DashboardOptions
        {
            SubscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID"),
            ResourceGroup = Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP"),
            AppName = Environment.GetEnvironmentVariable("CONTAINER_APP_NAME")
        };

        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<TokenCredential>(new DefaultAzureCredential());
        services.AddHttpClient<ContainerAppFunctionsClient>();
        services.AddHttpClient<AppInsightsClient>();

        return services;
    }

    public static IFunctionsWorkerApplicationBuilder UseContainerDashboardAuth(
        this IFunctionsWorkerApplicationBuilder builder)
    {
        builder.Use(next => async context =>
        {
            var httpContext = context.GetHttpContext();
            if (httpContext is null)
            {
                await next(context);
                return;
            }

            var options = context.InstanceServices.GetRequiredService<DashboardOptions>();
            var path = httpContext.Request.Path.Value ?? string.Empty;
            var prefix = "/" + options.RoutePrefix.TrimStart('/');

            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            if (!options.RequireAuthentication)
            {
                await next(context);
                return;
            }

            var principal = ClientPrincipalParser.Parse(httpContext.Request);

            if (principal is null)
            {
                httpContext.Response.StatusCode = 401;
                httpContext.Response.ContentType = "application/json";
                await httpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(new { error = "Authentication required. Please configure Easy Auth on your Container App." }));
                return;
            }

            if (options.AllowedRoles.Count > 0)
            {
                var userRoles = ClientPrincipalParser.GetUserRoles(principal);
                var hasRole = options.AllowedRoles.Any(r =>
                    userRoles.Contains(r, StringComparer.OrdinalIgnoreCase));

                if (!hasRole)
                {
                    httpContext.Response.StatusCode = 403;
                    httpContext.Response.ContentType = "application/json";
                    await httpContext.Response.WriteAsync(
                        JsonSerializer.Serialize(new { error = "Access denied. You do not have the required role to access this dashboard." }));
                    return;
                }
            }

            await next(context);
        });

        return builder;
    }
}
