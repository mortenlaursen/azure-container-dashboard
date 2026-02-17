# Research: Start and Stop Functions

**Branch**: `001-start-stop-functions` | **Date**: 2026-02-17

## R1: Azure ARM API for Enabling/Disabling Container App Functions

**Decision**: Use the Container Apps PATCH API to set environment variables that control function disabled state.

**Rationale**: The `Microsoft.App/containerApps/functions` sub-resource is entirely read-only in the `2025-10-02-preview` API. All properties including `isDisabled` are marked `readOnly: true` in the OpenAPI spec. There is no PUT, PATCH, POST, or action endpoint on the functions resource. The standard Azure Functions mechanism for disabling functions is to set an `AzureWebJobs.<FunctionName>.Disabled` environment variable, which on Container Apps requires PATCHing the parent Container App resource.

**Alternatives considered**:
- **Direct function resource update (PUT/PATCH on functions endpoint)**: Not available — the API defines zero write operations on functions.
- **Functions Extension proxy (`FunctionsExtension_InvokeFunctionsHost`)**: Proxies calls to the Functions host admin API. Marked as "internal Web RP communication" and not publicly documented. Not recommended for production.
- **Full Container App PUT**: Would work but is riskier — requires sending the full resource definition. PATCH (JSON Merge Patch) is safer as it only modifies specified fields.

## R2: Container App PATCH Endpoint Details

**Decision**: Use `PATCH` on the Container App resource with JSON Merge Patch content type.

**Rationale**: The Container Apps Update API (`PATCH /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.App/containerApps/{app}?api-version=2025-10-02-preview`) accepts `application/merge-patch+json`. This allows sending only the fields to modify.

**Key details**:
- `location` is a required field on the Container App resource, even for PATCH
- The `properties.template.containers` array is replaced entirely (arrays are not merged in JSON Merge Patch), so the full containers array must be sent
- This means a GET-then-PATCH pattern is needed: first GET the Container App to read the current template, then PATCH with the modified environment variables
- PATCHing the template section creates a new Container App revision

## R3: GET Container App Endpoint

**Decision**: Add a GET endpoint call for the Container App resource to retrieve the current template before PATCHing.

**Rationale**: To safely modify environment variables without losing existing ones, we need the current container definitions including all env vars. The GET endpoint returns the full resource.

**Endpoint**: `GET /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.App/containerApps/{app}?api-version=2025-10-02-preview`

## R4: Environment Variable Pattern

**Decision**: To disable function `MyFunc`, set env var `AzureWebJobs.MyFunc.Disabled` = `"true"`. To enable, set it to `"false"` or remove the env var.

**Rationale**: This is the standard Azure Functions mechanism documented by Microsoft. It works across all hosting models including Container Apps.

**Important consideration**: The function name in the env var must exactly match the function name as known to the Functions host, which corresponds to the `name` field from the functions list API.

## R5: Revision Creation Impact

**Decision**: Accept that enabling/disabling a function creates a new Container App revision, and communicate this clearly to the user.

**Rationale**: Any change to the Container App template triggers a new revision. This is an inherent behavior of the Azure Container Apps platform. The operation may take 10-30 seconds to propagate. The UI should inform the user that the change is being applied and may take a moment to take effect.

**Alternatives considered**:
- **Functions host admin API via proxy**: Could avoid revision creation but is undocumented/unsupported.
- There is no way to modify function state without creating a revision via the ARM API.

## R6: UI Pattern for Confirmation Dialogs

**Decision**: Use a Blazor inline confirmation pattern (show/hide confirmation UI within the table row) rather than a JavaScript `confirm()` dialog or a modal component.

**Rationale**: The project is a simple Blazor Server app with no component library or modal infrastructure. An inline confirmation (e.g., replacing the Stop button with "Are you sure? [Yes] [Cancel]") is the simplest approach that stays within pure Blazor without JavaScript interop, and fits the existing minimal UI patterns.

**Alternatives considered**:
- **JavaScript `confirm()` via IJSRuntime**: Works but mixes paradigms and blocks the UI thread.
- **Modal component library**: Over-engineered for this use case; would require adding a NuGet dependency.
- **Custom modal component**: More work than needed for a simple yes/no confirmation.
