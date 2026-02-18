using Azure.Container.Dashboard;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddContainerDashboard();
    })
    .Build();

host.Run();
