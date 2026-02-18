# API Contract: Application Insights Data-Plane

**Feature**: 002-function-invocations

## Authentication

**Token scope**: `https://api.applicationinsights.io/.default`
**Credential**: Same `AzureCliCredential` used for ARM, but with different scope.

## Extracting the App ID

Parse `APPLICATIONINSIGHTS_CONNECTION_STRING` from the container app's env vars:
```
InstrumentationKey=xxx;ApplicationId=<APP_ID>;IngestionEndpoint=...
```
Extract the `ApplicationId` value.

---

## Endpoint: Query Application Insights

```
POST https://api.applicationinsights.io/v1/apps/{appId}/query
```

### Headers

| Header | Value |
|--------|-------|
| Authorization | Bearer {token} |
| Content-Type | application/json |

### Request Body

```json
{
  "query": "<KQL query string>",
  "timespan": "P1D"
}
```

`timespan` uses ISO 8601 duration format: `PT1H` (1 hour), `P1D` (1 day), `P7D` (7 days), `P30D` (30 days).

### Response (200 OK)

```json
{
  "tables": [
    {
      "name": "PrimaryResult",
      "columns": [
        { "name": "columnName", "type": "string" }
      ],
      "rows": [
        ["value1", "value2"]
      ]
    }
  ]
}
```

### Error Response

```json
{
  "error": {
    "code": "BadArgumentError",
    "message": "Human-readable error message."
  }
}
```

---

## Query: Function Invocation List

Returns recent invocations for a specific function.

```kusto
requests
| extend functionNameFromCustomDimension = tostring(customDimensions['faas.name']),
         invocationId = coalesce(tostring(customDimensions['InvocationId']), tostring(customDimensions['faas.invocation_id']))
| where timestamp > ago(24h)
| where operation_Name =~ '{functionName}' or functionNameFromCustomDimension =~ '{functionName}'
| order by timestamp desc
| take 50
| project timestamp, success, resultCode, durationInMilliSeconds=duration, invocationId, operationId=operation_Id, operationName=operation_Name
```

### Response Columns

| Column | Type | Maps To |
|--------|------|---------|
| timestamp | datetime | FunctionInvocation.Timestamp |
| success | bool | FunctionInvocation.Success |
| resultCode | string | FunctionInvocation.ResultCode |
| durationInMilliSeconds | real | FunctionInvocation.DurationMs |
| invocationId | string | FunctionInvocation.InvocationId |
| operationId | string | FunctionInvocation.OperationId |
| operationName | string | FunctionInvocation.OperationName |

---

## Query: Invocation Trace Logs

Returns trace logs for a specific invocation, correlated by operation ID.

```kusto
traces
| where operation_Id == '{operationId}'
| order by timestamp asc
| project timestamp, message, severityLevel
```

### Response Columns

| Column | Type | Maps To |
|--------|------|---------|
| timestamp | datetime | InvocationTrace.Timestamp |
| message | string | InvocationTrace.Message |
| severityLevel | int | InvocationTrace.SeverityLevel |

### Severity Level Values

| Value | Label |
|-------|-------|
| 0 | Verbose |
| 1 | Information |
| 2 | Warning |
| 3 | Error |
| 4 | Critical |
