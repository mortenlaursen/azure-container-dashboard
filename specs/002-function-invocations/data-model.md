# Data Model: Function Invocation History

**Feature**: 002-function-invocations
**Date**: 2026-02-17

## Entities

### FunctionInvocation

Represents a single execution of a function, sourced from Application Insights `requests` table.

| Field | Type | Source | Description |
|-------|------|--------|-------------|
| Timestamp | DateTime | `timestamp` | When the invocation started |
| Success | bool | `success` | Whether the invocation completed successfully |
| ResultCode | string | `resultCode` | HTTP status code or result code (e.g., "200", "500") |
| DurationMs | double | `duration` | Execution time in milliseconds |
| InvocationId | string | `customDimensions['InvocationId']` or `customDimensions['faas.invocation_id']` | Unique ID for this function execution |
| OperationId | string | `operation_Id` | Correlation ID used to find related traces |
| OperationName | string | `operation_Name` | Function name as reported by the runtime |

### InvocationTrace

Represents a single log entry within a function execution, sourced from Application Insights `traces` table.

| Field | Type | Source | Description |
|-------|------|--------|-------------|
| Timestamp | DateTime | `timestamp` | When the log entry was written |
| Message | string | `message` | Log message text |
| SeverityLevel | int | `severityLevel` | 0=Verbose, 1=Information, 2=Warning, 3=Error, 4=Critical |

### AppInsightsQueryResponse

Generic response wrapper from the Application Insights query API.

| Field | Type | Description |
|-------|------|-------------|
| Tables | List&lt;AppInsightsTable&gt; | Result tables (typically one: "PrimaryResult") |

### AppInsightsTable

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Table name (e.g., "PrimaryResult") |
| Columns | List&lt;AppInsightsColumn&gt; | Column definitions |
| Rows | List&lt;List&lt;JsonElement&gt;&gt; | Row data as arrays matching column order |

### AppInsightsColumn

| Field | Type | Description |
|-------|------|-------------|
| Name | string | Column name |
| Type | string | Data type (e.g., "datetime", "bool", "string", "real") |

## Relationships

- A **ContainerAppFunction** (existing entity) has many **FunctionInvocation** records (queried by function name).
- A **FunctionInvocation** has many **InvocationTrace** records (queried by operation ID).
- The link between a ContainerApp and Application Insights is via the `APPLICATIONINSIGHTS_CONNECTION_STRING` env var on the container.

## State Transitions

FunctionInvocation does not have managed state — it is read-only telemetry data. The `Success` field reflects the final outcome:
- `true` = completed successfully
- `false` = failed (check `ResultCode` and traces for details)
