# Quickstart: Enable Authentication for Container App Dashboard

## Prerequisites

- Azure Container App with the dashboard NuGet package installed
- Azure Entra ID tenant (standard with any Azure subscription)
- Azure CLI installed (or use Azure Portal)

## Step 1: Enable In-App Authentication Enforcement

Update your `Program.cs` to require authentication:

```csharp
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(app =>
    {
        app.UseContainerDashboardAuth();
    })
    .ConfigureServices(services =>
    {
        services.AddContainerDashboard(options =>
        {
            options.RequireAuthentication = true;
        });
    })
    .Build();

host.Run();
```

Optional: restrict to specific roles:

```csharp
services.AddContainerDashboard(options =>
{
    options.RequireAuthentication = true;
    options.AllowedRoles = ["Dashboard.Admin"];
});
```

## Step 2: Enable Easy Auth on Your Container App

### Option A: Azure Portal (Easiest)

1. Navigate to your Container App in Azure Portal
2. Select **Authentication** in the left menu
3. Click **Add identity provider**
4. Select **Microsoft**
5. The portal will auto-create an Entra ID app registration
6. Set **Restrict access** to "Require authentication"
7. Set **Unauthenticated requests** to "HTTP 302 Found redirect: recommended for websites"
8. Click **Add**

### Option B: Azure CLI

```bash
# Create Entra ID app registration
az ad app create \
  --display-name "My Dashboard" \
  --sign-in-audience AzureADMyOrg

# Note the appId from output, then:
az ad app credential reset --id <APP_ID>

# Enable Easy Auth on your Container App
az containerapp auth microsoft update \
  --name <CONTAINER_APP_NAME> \
  --resource-group <RESOURCE_GROUP> \
  --client-id <APP_ID> \
  --client-secret <CLIENT_SECRET> \
  --tenant-id <TENANT_ID> \
  --yes

az containerapp auth update \
  --name <CONTAINER_APP_NAME> \
  --resource-group <RESOURCE_GROUP> \
  --unauthenticated-client-action RedirectToLoginPage \
  --redirect-provider azureactivedirectory \
  --enabled true
```

## Step 3: Deploy and Test

1. Deploy your updated Functions app
2. Navigate to `https://<your-app>.azurecontainerapps.io/dashboard`
3. You should be redirected to Microsoft sign-in
4. After signing in, the dashboard loads with your name displayed in the header
5. Click "Sign out" to end your session

## How It Works

```
Browser → Container Apps Easy Auth (sidecar) → Your Functions App
                    │                                    │
                    ├─ Handles sign-in redirect           ├─ Checks X-MS-CLIENT-PRINCIPAL header
                    ├─ Validates tokens                   ├─ Displays user info in dashboard
                    ├─ Manages sessions (cookies)         ├─ Enforces role restrictions (optional)
                    └─ Injects identity headers            └─ Returns 401/403 if no identity
```

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Dashboard returns 401 but Easy Auth is not set up | Either enable Easy Auth (Step 2) or set `RequireAuthentication = false` |
| Can sign in but get "Access Denied" | Check that your user has the required role if `AllowedRoles` is configured |
| Sign-in redirects to wrong URL | Verify the redirect URI in your Entra ID app registration matches `https://<your-app>/.auth/login/aad/callback` |
| No user name shown in dashboard | Ensure the token store is enabled in Easy Auth settings |
