using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;

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
}
