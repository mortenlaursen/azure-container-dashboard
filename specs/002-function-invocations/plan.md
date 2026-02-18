# Implementation Plan: Function Invocation History

**Branch**: `002-function-invocations` | **Date**: 2026-02-17 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-function-invocations/spec.md`

## Summary

Add per-function invocation history and trace drill-down to the Container App Functions dashboard. Users click a function name to see recent invocations (timestamp, status, duration), then click an invocation to see its trace logs. Data is queried from Application Insights using the same KQL queries as the Azure CLI `az containerapp function invocations` commands.

## Technical Context

**Language/Version**: C# / .NET 9.0
**Primary Dependencies**: Azure.Identity 1.17.1, System.Text.Json 10.0.3, Blazor Server (built-in)
**Storage**: N/A (read-only queries against Application Insights)
**Testing**: Manual testing against live Azure resources
**Target Platform**: Linux server (Blazor Server-side rendering)
**Project Type**: Web (single project, Blazor Server)
**Performance Goals**: Invocation list loads within 5 seconds
**Constraints**: Requires Application Insights connected to the container app
**Scale/Scope**: Single-user dashboard, 50 invocations per page, single KQL query per view

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constitution is unconfigured (template placeholders only). No gates to evaluate. Proceeding.

**Post-design re-check**: No constitution violations. Design follows existing patterns in the codebase (same client architecture, same Blazor page pattern, same credential approach).

## Project Structure

### Documentation (this feature)

```text
specs/002-function-invocations/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── application-insights-api.md
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
AzureContainerFunctions/
├── AppInsightsClient.cs              # NEW: Application Insights query client
├── AppInsightsModels.cs              # NEW: AI response types + domain entities
├── ContainerAppFunction.cs           # EXISTING: models (no changes needed)
├── ContainerAppFunctionsClient.cs    # MODIFIED: add App ID extraction helper
├── Program.cs                        # MODIFIED: register AppInsightsClient
├── Components/
│   ├── Pages/
│   │   ├── Home.razor                # MODIFIED: function names become links
│   │   └── FunctionDetail.razor      # NEW: invocation list + trace drill-down
│   └── Layout/
│       └── MainLayout.razor          # EXISTING: no changes
└── wwwroot/
    └── css/
        └── app.css                   # MODIFIED: invocation/trace styles
```

**Structure Decision**: Single Blazor Server project (matches existing). New files follow the flat structure already established (client classes at root, pages under Components/Pages). No new projects or directories beyond what exists.

## Design Decisions

### 1. Separate Client for Application Insights

A new `AppInsightsClient` handles Application Insights data-plane queries. This is separate from `ContainerAppFunctionsClient` because:
- Different API endpoint (`api.applicationinsights.io` vs `management.azure.com`)
- Different token scope (`https://api.applicationinsights.io/.default`)
- Different concern (telemetry queries vs resource management)

### 2. App ID Discovery

The Application Insights App ID is extracted automatically from the container app's `APPLICATIONINSIGHTS_CONNECTION_STRING` env var. This is the same approach the Azure CLI uses. No manual configuration needed from the user.

Flow:
1. User navigates to function detail (subscription/RG/app already known from Home page)
2. `ContainerAppFunctionsClient.GetContainerAppAsync()` fetches the container definition
3. Parse `APPLICATIONINSIGHTS_CONNECTION_STRING` from containers[0].env
4. Extract `ApplicationId` value
5. Pass to `AppInsightsClient` for queries

### 3. Navigation via Query Parameters

Function detail page uses URL query parameters to receive context:
```
/function/{functionName}?sub={subscriptionId}&rg={resourceGroup}&app={appName}
```

This preserves browser back button functionality and allows deep linking. On return to Home, query params restore the search context.

### 4. Trace Drill-down as Inline Expand

Rather than a third page, clicking an invocation row expands an inline panel showing traces. This keeps the context visible (the invocation list) and reduces navigation depth, matching SC-002 (3 clicks or fewer: list -> function -> expand traces).

## Complexity Tracking

No constitution violations to justify.
