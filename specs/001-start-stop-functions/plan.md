# Implementation Plan: Start and Stop Functions

**Branch**: `001-start-stop-functions` | **Date**: 2026-02-17 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-start-stop-functions/spec.md`

## Summary

Add the ability for operators to start (enable) and stop (disable) individual Azure Container App functions from the existing function list UI. Since the functions ARM API is read-only (`isDisabled` is a read-only property), the implementation uses the Container App PATCH API to set `AzureWebJobs.<FunctionName>.Disabled` environment variables on the parent Container App resource. The UI adds contextual action buttons per function row with inline confirmation for stop actions, per-row loading states, and error handling.

## Technical Context

**Language/Version**: C# / .NET 9.0
**Primary Dependencies**: Azure.Identity 1.17.1, System.Text.Json 10.0.3, Blazor Server (built-in)
**Storage**: N/A (stateless — reads/writes via Azure ARM REST API)
**Testing**: Manual testing against Azure subscription (no test framework currently configured)
**Target Platform**: Web (Blazor Interactive Server-Side Rendering)
**Project Type**: Single project — Blazor web application
**Performance Goals**: Start/stop operations complete within 10 seconds (including Azure API round-trip and revision creation)
**Constraints**: Operations create a new Container App revision (Azure platform behavior); requires Azure credentials with write access to the Container App resource
**Scale/Scope**: Single-page application, 4 files modified, ~150 lines of new code

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The project constitution is not yet configured (template placeholders only). No gates to evaluate. Proceeding with standard engineering practices.

**Post-Phase 1 re-check**: No violations. The design follows the existing project patterns:
- New models are added to the existing models file
- New client methods follow the same pattern as existing ones
- UI changes extend the existing page component
- No new dependencies or architectural changes

## Project Structure

### Documentation (this feature)

```text
specs/001-start-stop-functions/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0: API research findings
├── data-model.md        # Phase 1: Data model definitions
├── quickstart.md        # Phase 1: Implementation quickstart
├── contracts/
│   └── azure-arm-api.md # Phase 1: API contracts
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
AzureContainerFunctions/
├── ContainerAppFunction.cs         # Models (existing + new Container App models)
├── ContainerAppFunctionsClient.cs  # API client (existing + new PATCH methods)
├── Components/
│   └── Pages/
│       └── Home.razor              # UI (existing + action buttons, confirmation, loading)
└── wwwroot/
    └── css/
        └── app.css                 # Styles (existing + new button/state styles)
```

**Structure Decision**: This is a single Blazor project with no separate frontend/backend split. All changes are made within the existing flat project structure. No new files are created — only existing files are modified.

## Design Decisions

### D1: GET-then-PATCH pattern for environment variable updates

The Container App PATCH API uses JSON Merge Patch, which replaces arrays entirely rather than merging them. This means the `containers` array must be sent in full. The implementation must:

1. GET the current Container App to read `location` and `properties.template.containers`
2. Find or add the `AzureWebJobs.<FunctionName>.Disabled` env var on the appropriate container
3. PATCH with the complete modified containers array

This ensures existing environment variables, container config, and other settings are preserved.

### D2: Inline confirmation for stop actions

Stop (disable) actions use an inline confirmation pattern within the table row — the Stop button is replaced with "Confirm" and "Cancel" buttons. This avoids JavaScript interop (`IJSRuntime`), stays within pure Blazor, and matches the application's minimal UI approach.

Start (enable) actions execute immediately without confirmation, as enabling a function is non-destructive.

### D3: Per-row loading state tracking

A `HashSet<string>` of function names tracks which functions have operations in progress. This allows:
- Disabling action buttons for functions with active operations
- Showing a loading indicator on the specific row
- Allowing operations on other functions concurrently

### D4: Optimistic UI update after successful PATCH

After a successful PATCH, the local `IsDisabled` property is toggled immediately rather than re-fetching the function list. This provides instant feedback. The actual API state may take a moment to converge (new revision deployment), but the user sees the expected result immediately.

### D5: Partial Container App model

Only the fields needed for GET/PATCH are modeled (`location`, `template.containers[].name/image/env/resources`). The `JsonSerializerOptions` will use `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` to avoid sending null fields in the PATCH body. Unknown properties from the GET response are ignored during deserialization.

## Implementation Steps

### Step 1: Add Container App models to `ContainerAppFunction.cs`

Add the following classes with `[JsonPropertyName]` attributes:
- `ContainerApp` with `Location` and `Properties`
- `ContainerAppProperties` with `Template`
- `ContainerAppTemplate` with `Containers`
- `ContainerAppContainer` with `Name`, `Image`, `Env`, `Resources` (JsonElement for passthrough)
- `EnvironmentVariable` with `Name`, `Value`, `SecretRef`

Maps to: FR-001 (system needs to know function state to show correct actions)

### Step 2: Add PATCH capability to `ContainerAppFunctionsClient.cs`

- Add `GetContainerAppAsync()` method (GET the Container App resource)
- Add a `SendPatchRequestAsync()` private method that sends PATCH with JSON body
- Add `SetFunctionDisabledAsync(subscriptionId, resourceGroup, appName, functionName, disabled)` that:
  1. Calls `GetContainerAppAsync()` to read current state
  2. Finds/adds the `AzureWebJobs.<FunctionName>.Disabled` env var
  3. Calls `SendPatchRequestAsync()` with the modified Container App

Maps to: FR-001, FR-007, FR-008, FR-009

### Step 3: Add UI controls to `Home.razor`

- Add an "Actions" column to the table header
- For each function row, show a Start or Stop button based on `IsDisabled`
- Add inline confirmation state tracking (`confirmingStop` set of function names)
- Add operation-in-progress state tracking (`operationsInProgress` set of function names)
- Add `successMessage` string for success feedback
- Implement `StopFunction(functionName)`, `ConfirmStop(functionName)`, `CancelStop(functionName)`, `StartFunction(functionName)` methods
- Show loading indicator and disable buttons during operations
- Show error/success messages

Maps to: FR-001 through FR-009, all acceptance scenarios

### Step 4: Add CSS styles to `app.css`

- `.btn-danger` for Stop button (red theme)
- `.btn-success` for Start button (green theme)
- `.btn-sm` for small action buttons
- `.btn-confirm` / `.btn-cancel` for confirmation buttons
- `.row-loading` for loading state indicator on table rows
- `.success-box` for success message display
- `.actions-cell` for the actions column layout

Maps to: FR-005, SC-003, SC-005
