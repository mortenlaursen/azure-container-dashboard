# Quickstart: Start and Stop Functions

**Branch**: `001-start-stop-functions` | **Date**: 2026-02-17

## Overview

This feature adds the ability to start (enable) and stop (disable) Azure Container App functions from the existing function list UI. Since the functions ARM API is read-only, this works by PATCHing the parent Container App's environment variables to set `AzureWebJobs.<FunctionName>.Disabled`.

## Files to Modify

### 1. `ContainerAppFunction.cs` — Add Container App models
Add partial models for the Container App resource (only the fields needed for GET/PATCH):
- `ContainerApp` — top-level resource with `Location` and `Properties`
- `ContainerAppProperties` — contains `Template`
- `ContainerAppTemplate` — contains `Containers` list
- `ContainerAppContainer` — has `Name`, `Image`, `Env`, `Resources`
- `EnvironmentVariable` — has `Name`, `Value`, `SecretRef`

### 2. `ContainerAppFunctionsClient.cs` — Add enable/disable method
Add three new methods:
- `GetContainerAppAsync()` — GET the Container App resource
- `SetFunctionDisabledAsync()` — orchestrates the GET-modify-PATCH flow
- Extend `SendRequestAsync` or add a new method for PATCH requests with a request body

### 3. `Components/Pages/Home.razor` — Add UI controls
Modify the existing function table to include:
- An "Actions" column with Start/Stop buttons per row
- Inline confirmation UI for Stop actions
- Per-row loading state tracking
- Success/error message display
- Button disabled state during in-progress operations

### 4. `wwwroot/css/app.css` — Add styles for new UI elements
Add styles for:
- Action buttons (start/stop)
- Inline confirmation UI
- Loading spinner/indicator on table rows
- Success message styling

## Implementation Order

1. **Models first** (`ContainerAppFunction.cs`): Add the Container App models needed for deserialization
2. **Client methods** (`ContainerAppFunctionsClient.cs`): Add GET Container App and PATCH methods
3. **UI** (`Home.razor`): Add action buttons, confirmation, loading state, and error handling
4. **Styles** (`app.css`): Add CSS for new elements

## Key Design Decisions

- **GET-then-PATCH pattern**: Must read the current Container App state before modifying, to preserve existing env vars and container config
- **Inline confirmation**: Stop actions use inline "Are you sure?" UI within the table row, avoiding JavaScript interop
- **Per-row state tracking**: Each function row independently tracks whether an operation is in progress, using a `HashSet<string>` of function names
- **Optimistic UI update**: After a successful PATCH, toggle the local `IsDisabled` value immediately rather than re-fetching the entire function list (the change takes effect with a new revision)
- **No new revision**: The user is informed that changes create a new Container App revision and may take a moment to propagate
