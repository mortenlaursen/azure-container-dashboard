namespace Azure.Container.Dashboard;

public class DashboardOptions
{
    public string? SubscriptionId { get; set; }
    public string? ResourceGroup { get; set; }
    public string? AppName { get; set; }
    public string RoutePrefix { get; set; } = "dashboard";
}
