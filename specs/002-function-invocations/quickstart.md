# Quickstart: Function Invocation History

**Feature**: 002-function-invocations

## Prerequisites

- .NET 9.0 SDK
- Azure CLI authenticated (`az login`)
- A Container App with Azure Functions that has Application Insights connected

## What Gets Added

### New Files

| File | Purpose |
|------|---------|
| `AppInsightsClient.cs` | Client for querying Application Insights data-plane API |
| `AppInsightsModels.cs` | Response/entity models for AI query results (FunctionInvocation, InvocationTrace) |
| `Components/Pages/FunctionDetail.razor` | Function detail page with invocation summary table and trace drill-down |

### Modified Files

| File | Change |
|------|--------|
| `ContainerAppFunctionsClient.cs` | Add method to extract Application Insights App ID from container env vars |
| `Program.cs` | Register `AppInsightsClient` in DI |
| `Components/Pages/Home.razor` | Make function names clickable links to the detail page |
| `wwwroot/css/app.css` | Styles for invocation table, trace panel, severity badges |

## Implementation Order

1. **Models** — Add `AppInsightsModels.cs` with query response types and domain entities
2. **Client** — Add `AppInsightsClient.cs` with KQL query methods for invocations and traces
3. **Extract App ID** — Add helper to parse `APPLICATIONINSIGHTS_CONNECTION_STRING` from container env vars
4. **DI Registration** — Register `AppInsightsClient` as singleton in `Program.cs`
5. **Detail Page** — Create `FunctionDetail.razor` with invocation summary table
6. **Trace Drill-down** — Add expandable trace view within the detail page
7. **Navigation** — Update `Home.razor` to link function names to the detail page with query params
8. **Styles** — Add CSS for new components

## Architecture Notes

- **Two different token scopes**: ARM calls use `https://management.azure.com/.default`, Application Insights calls use `https://api.applicationinsights.io/.default`. Both use `AzureCliCredential`.
- **App ID discovery is automatic**: The system reads `APPLICATIONINSIGHTS_CONNECTION_STRING` from the container's env vars (already fetched via ARM) and parses out the `ApplicationId`.
- **No new NuGet packages needed**: The existing `Azure.Identity` and `System.Text.Json` packages plus built-in `HttpClient` are sufficient.
