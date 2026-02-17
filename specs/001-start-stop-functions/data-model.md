# Data Model: Start and Stop Functions

**Branch**: `001-start-stop-functions` | **Date**: 2026-02-17

## Existing Entities (Unchanged)

### ContainerAppFunction
Represents a function within a Container App. Read from the ARM API.

| Field | Type | Description |
|-------|------|-------------|
| Id | string | ARM resource ID |
| Name | string | Function name |
| Type | string | ARM resource type |
| Properties | ContainerAppFunctionProperties | Function metadata |

### ContainerAppFunctionProperties
Metadata about a function.

| Field | Type | Description |
|-------|------|-------------|
| InvokeUrlTemplate | string? | HTTP endpoint URL |
| TriggerType | string? | Trigger mechanism (e.g., httpTrigger) |
| Language | string? | Programming language |
| IsDisabled | bool | Whether the function is disabled (read-only from API) |

### ContainerAppFunctionCollection
Paginated list of functions.

| Field | Type | Description |
|-------|------|-------------|
| Value | List\<ContainerAppFunction\> | Functions in current page |
| NextLink | string? | URL for next page |

## New Entities

### ContainerApp (partial, for PATCH operations)
Represents the parent Container App resource. Only the fields needed for the enable/disable PATCH operation are modeled.

| Field | Type | Description |
|-------|------|-------------|
| Location | string | Azure region (required for PATCH) |
| Properties | ContainerAppProperties | Container App properties |

### ContainerAppProperties
| Field | Type | Description |
|-------|------|-------------|
| Template | ContainerAppTemplate | Template definition containing containers |

### ContainerAppTemplate
| Field | Type | Description |
|-------|------|-------------|
| Containers | List\<ContainerAppContainer\> | Container definitions |

### ContainerAppContainer
| Field | Type | Description |
|-------|------|-------------|
| Name | string | Container name |
| Image | string | Container image |
| Env | List\<EnvironmentVariable\>? | Environment variables |
| Resources | object? | Resource allocations (passthrough, preserve on PATCH) |

### EnvironmentVariable
| Field | Type | Description |
|-------|------|-------------|
| Name | string | Variable name (e.g., `AzureWebJobs.MyFunc.Disabled`) |
| Value | string? | Variable value |
| SecretRef | string? | Reference to a secret (mutually exclusive with Value) |

## State Transitions

### Function Enable/Disable Flow

```
Active (IsDisabled=false)
    │
    ▼ [User clicks Stop + Confirms]
    │
Disabling... (UI loading state)
    │
    ▼ [PATCH Container App: set AzureWebJobs.<Name>.Disabled=true]
    │
    ├── Success → Disabled (IsDisabled=true)
    │               │
    │               ▼ [User clicks Start]
    │               │
    │             Enabling... (UI loading state)
    │               │
    │               ▼ [PATCH Container App: set AzureWebJobs.<Name>.Disabled=false]
    │               │
    │               ├── Success → Active (IsDisabled=false)
    │               └── Failure → Disabled (show error)
    │
    └── Failure → Active (show error, status unchanged)
```

## Key Relationships

- A **Container App** contains one or more **Containers**, each with **Environment Variables**
- A **Container App** has **Functions** exposed via the functions sub-resource (read-only)
- The `AzureWebJobs.<FunctionName>.Disabled` environment variable controls the `IsDisabled` state of a function
- Changing environment variables on the Container App triggers a **new revision**
