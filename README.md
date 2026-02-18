# Azure.Container.Dashboard.Functions

A NuGet package that adds a web dashboard to your Azure Functions app for managing Azure Container Apps — start/stop, enable/disable functions, and monitor invocations via Application Insights.

## Screenshots

<!-- TODO: Add screenshots -->
![Dashboard overview](docs/screenshots/dashboard-overview.png)

![Function detail](docs/screenshots/function-detail.png)

## Installation

```bash
dotnet add package Azure.Container.Dashboard.Functions
```

## Setup

### 1. Register the services

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddContainerDashboard();
    })
    .Build();

host.Run();
```

### 2. Configure environment variables

| Variable | Required | Description |
|---|---|---|
| `AZURE_SUBSCRIPTION_ID` | Yes | Azure subscription containing the container app |
| `AZURE_RESOURCE_GROUP` | Yes | Resource group name |
| `CONTAINER_APP_NAME` | Yes | Container App name |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | For invocations | Enables invocation tracking |

Authentication uses `DefaultAzureCredential` (Managed Identity, Azure CLI, etc.).

## Features

- **App control** — Start and stop your Container App
- **Function management** — List, enable, and disable individual functions
- **Invocation monitoring** — View invocation history, counts, and traces via App Insights
- **Embedded UI** — Static HTML dashboard served at `/dashboard`

## API Endpoints

All routes are under `/dashboard`:

| Method | Route | Description |
|---|---|---|
| GET | `/dashboard` | Dashboard UI |
| GET | `/dashboard/status` | Container app status |
| POST | `/dashboard/app/start` | Start app |
| POST | `/dashboard/app/stop` | Stop app |
| GET | `/dashboard/functions` | List functions |
| POST | `/dashboard/functions/update` | Enable/disable functions |
| GET | `/dashboard/invocations/{name}` | Invocation history |
| GET | `/dashboard/invocations/{name}/counts` | Invocation counts |
| GET | `/dashboard/traces/{operationId}` | Execution traces |
