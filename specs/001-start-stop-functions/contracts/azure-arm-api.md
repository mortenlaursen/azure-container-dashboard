# API Contracts: Azure ARM REST API Operations

**Branch**: `001-start-stop-functions` | **Date**: 2026-02-17

## Existing Operations (No Changes)

### List Functions
```
GET /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.App/containerApps/{containerAppName}/functions?api-version=2025-10-02-preview

Authorization: Bearer {token}

Response 200:
{
  "value": [
    {
      "id": "string",
      "name": "string",
      "type": "string",
      "properties": {
        "invokeUrlTemplate": "string",
        "triggerType": "string",
        "language": "string",
        "isDisabled": boolean
      }
    }
  ],
  "nextLink": "string | null"
}
```

### Get Function
```
GET /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.App/containerApps/{containerAppName}/functions/{functionName}?api-version=2025-10-02-preview

Authorization: Bearer {token}

Response 200:
{
  "id": "string",
  "name": "string",
  "type": "string",
  "properties": {
    "invokeUrlTemplate": "string",
    "triggerType": "string",
    "language": "string",
    "isDisabled": boolean
  }
}
```

## New Operations

### Get Container App (needed to read current template before PATCH)
```
GET /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.App/containerApps/{containerAppName}?api-version=2025-10-02-preview

Authorization: Bearer {token}

Response 200:
{
  "location": "string",
  "properties": {
    "template": {
      "containers": [
        {
          "name": "string",
          "image": "string",
          "env": [
            {
              "name": "string",
              "value": "string",
              "secretRef": "string"
            }
          ],
          "resources": { ... }
        }
      ]
    },
    ... (other properties preserved but not modeled)
  }
}
```

### Update Container App (PATCH to set function disabled env var)
```
PATCH /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.App/containerApps/{containerAppName}?api-version=2025-10-02-preview

Authorization: Bearer {token}
Content-Type: application/merge-patch+json

Request Body:
{
  "location": "{currentLocation}",
  "properties": {
    "template": {
      "containers": [
        {
          "name": "{containerName}",
          "image": "{containerImage}",
          "env": [
            // ... all existing env vars ...
            {
              "name": "AzureWebJobs.{functionName}.Disabled",
              "value": "true"   // or "false" to enable
            }
          ],
          "resources": { ... preserve existing ... }
        }
        // ... all containers in the template ...
      ]
    }
  }
}

Response 200:
{
  // Full Container App resource
}

Error Responses:
  403: Insufficient permissions
  404: Container App not found
  409: Conflict (concurrent modification)
  429: Rate limited
```

## Client Method Contracts

### SetFunctionDisabledAsync
**Purpose**: Enable or disable a specific function by updating the Container App environment variables.

**Parameters**:
| Parameter | Type | Description |
|-----------|------|-------------|
| subscriptionId | string | Azure subscription ID |
| resourceGroupName | string | Resource group name |
| containerAppName | string | Container App name |
| functionName | string | Function name to enable/disable |
| disabled | bool | `true` to disable, `false` to enable |
| cancellationToken | CancellationToken | Cancellation token |

**Flow**:
1. GET the Container App to read current template (location, containers, env vars)
2. Find or create the `AzureWebJobs.{functionName}.Disabled` env var in the first container
3. Set value to `"true"` (disable) or `"false"` (enable)
4. PATCH the Container App with the modified template
5. Return success or throw on failure

**Error cases**:
- Container App not found → `HttpRequestException` with 404
- Permission denied → `HttpRequestException` with 403
- Concurrent modification → `HttpRequestException` with 409
- Network failure → `HttpRequestException`
