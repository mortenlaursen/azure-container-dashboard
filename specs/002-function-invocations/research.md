# Research: Function Invocation History

**Feature**: 002-function-invocations
**Date**: 2026-02-17

## Decision 1: Data Source for Invocations

**Decision**: Query Application Insights data-plane API directly (`https://api.applicationinsights.io/v1/apps/{appId}/query`), not the ARM resource provider.

**Rationale**: This is exactly what the `az containerapp function invocations` CLI commands do. The ARM Container Apps RP does not expose invocation data — it only manages the container app resource. The invocation telemetry lives in Application Insights, which is the standard telemetry backend for Azure Functions on Container Apps.

**Alternatives considered**:
- ARM RP endpoints: Do not exist for invocations.
- Direct Log Analytics Workspace query: More complex setup, requires workspace ID instead of app ID. Application Insights API is simpler and matches the CLI approach.

## Decision 2: How to Obtain the Application Insights App ID

**Decision**: Extract the `ApplicationId` from the `APPLICATIONINSIGHTS_CONNECTION_STRING` environment variable on the container app's revision. This is the same approach the Azure CLI uses.

**Rationale**: The connection string is already present on the container app's containers as an environment variable. We already have `GetContainerAppAsync()` which returns the full container definition including env vars. The `ApplicationId` field in the connection string is what the Application Insights data-plane API needs.

**Alternatives considered**:
- Manual configuration (user enters App ID): Extra friction, error-prone.
- ARM query for Application Insights resource: Requires knowing the AI resource name/RG, adds complexity.

## Decision 3: Authentication Scope for Application Insights

**Decision**: Use a separate token with scope `https://api.applicationinsights.io/.default` for Application Insights calls. The existing ARM token (`https://management.azure.com/.default`) will not work for the Application Insights data-plane.

**Rationale**: Application Insights data-plane is a different API with a different audience. Azure CLI does the same — it acquires a separate token for AI queries.

**Alternatives considered**:
- Reusing the ARM token: Will be rejected by the AI API (wrong audience).

## Decision 4: KQL Queries

**Decision**: Use the same KQL queries as the Azure CLI extension for both invocation summary and traces.

**Rationale**: These queries are battle-tested and match the exact data schema that Azure Functions on Container Apps writes to Application Insights. They handle both classic (`InvocationId`) and OpenTelemetry (`faas.invocation_id`) custom dimensions.

**Key queries**:

### Invocation Traces (per function)
```kusto
requests
| extend functionNameFromCustomDimension = tostring(customDimensions['faas.name']),
         invocationId = coalesce(tostring(customDimensions['InvocationId']), tostring(customDimensions['faas.invocation_id']))
| where timestamp > ago({timespan})
| where isempty("{revisionFilter}") or cloud_RoleInstance contains '{revisionName}'
| where operation_Name =~ '{functionName}' or functionNameFromCustomDimension =~ '{functionName}'
| order by timestamp desc
| take {limit}
| project timestamp, success, resultCode, durationInMilliSeconds=duration, invocationId, operationId=operation_Id, operationName=operation_Name
```

### Trace Logs (per invocation, via operationId)
```kusto
traces
| where operation_Id == '{operationId}'
| order by timestamp asc
| project timestamp, message, severityLevel
```

## Decision 5: Navigation Pattern

**Decision**: Use Blazor page routing. Function detail view will be a new Razor page at `/function/{name}` that receives context via query parameters or a shared state service.

**Rationale**: Blazor Server already supports multi-page routing via `@page` directives. This gives us URL-based navigation and browser back button support. Search parameters (subscription, RG, app name) can be passed via query string to preserve context.

**Alternatives considered**:
- In-page expand/collapse: Simpler but doesn't support deep linking or browser navigation.
- Modal/dialog: Poor UX for detailed trace data that can be lengthy.
