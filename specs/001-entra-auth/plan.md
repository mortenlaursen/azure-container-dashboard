# Implementation Plan: Entra ID Authentication for Dashboard

**Branch**: `001-entra-auth` | **Date**: 2026-02-19 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-entra-auth/spec.md`

## Summary

Add authentication support to the Container App Dashboard so that deployments not behind a VNet can restrict access to authorized users. The approach uses Azure Container Apps Built-in Authentication (Easy Auth) as the platform-level sign-in mechanism, with optional in-app enforcement via middleware that validates the `X-MS-CLIENT-PRINCIPAL` header on all dashboard routes. The dashboard UI is updated to display the signed-in user and provide a sign-out link.

## Technical Context

**Language/Version**: C# / .NET 9.0
**Primary Dependencies**: Microsoft.Azure.Functions.Worker v2.0.0, Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore v2.0.0, Azure.Identity v1.17.1, System.Text.Json v10.0.3
**Storage**: N/A (stateless — sessions managed by Easy Auth platform)
**Testing**: xUnit (to be added for new middleware/parser code)
**Target Platform**: Azure Container Apps (Linux containers)
**Project Type**: NuGet package (single project)
**Performance Goals**: Authentication check adds <5ms overhead per request (header parsing only)
**Constraints**: Zero new NuGet dependencies; backward compatible with existing deployments; no breaking changes to public API
**Scale/Scope**: 4 modified files, 2 new files, ~300 lines of new code

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The project constitution is a template with no project-specific gates defined. No violations to check.

**Pre-research gate**: PASS (no constitution violations)
**Post-design gate**: PASS (no constitution violations)

## Project Structure

### Documentation (this feature)

```text
specs/001-entra-auth/
├── plan.md              # This file
├── research.md          # Authentication approach decisions
├── data-model.md        # Entity definitions (ClientPrincipal, auth options)
├── quickstart.md        # Setup guide for developers
├── contracts/
│   ├── auth-middleware.md       # Middleware behavior contract
│   ├── dashboard-ui-auth.md     # Frontend auth UI contract
│   └── client-principal-parser.md # Header parser contract
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/Azure.Container.Dashboard.Functions/
├── Azure.Container.Dashboard.Functions.csproj  # No changes (no new dependencies)
├── DashboardOptions.cs                          # MODIFIED: Add RequireAuthentication, AllowedRoles
├── ServiceCollectionExtensions.cs               # MODIFIED: Register middleware
├── Auth/
│   ├── DashboardAuthMiddleware.cs               # NEW: ASP.NET Core middleware for auth enforcement
│   └── ClientPrincipalParser.cs                 # NEW: Parse X-MS-CLIENT-PRINCIPAL header
├── Functions/
│   ├── DashboardUiFunction.cs                   # MODIFIED: Pass user info to HTML response
│   ├── AppManagementFunctions.cs                # No changes (middleware handles auth)
│   ├── FunctionManagementFunctions.cs           # No changes (middleware handles auth)
│   └── InvocationFunctions.cs                   # No changes (middleware handles auth)
├── Clients/                                     # No changes
├── Models/                                      # No changes
└── wwwroot/
    └── dashboard.html                           # MODIFIED: Add user display, logout, auth error handling

test/Azure.Container.Dashboard.Tests/            # NEW: Test project (if not exists)
├── Auth/
│   ├── ClientPrincipalParserTests.cs            # Unit tests for header parser
│   └── DashboardAuthMiddlewareTests.cs          # Unit tests for middleware
└── Azure.Container.Dashboard.Tests.csproj
```

**Structure Decision**: Follows the existing single-project structure. New auth files go in an `Auth/` subfolder within the existing project. No new projects or solutions needed. Test project added if not already present.

## Implementation Components

### Component 1: DashboardOptions Extension

**File**: `DashboardOptions.cs`
**Change**: Add two new properties

```csharp
public bool RequireAuthentication { get; set; } = false;
public List<string> AllowedRoles { get; set; } = [];
```

Backward compatible: defaults to `false` and empty list.

### Component 2: ClientPrincipalParser

**File**: `Auth/ClientPrincipalParser.cs` (new)
**Purpose**: Parse the `X-MS-CLIENT-PRINCIPAL` header into a `ClaimsPrincipal`

Key behaviors:
- Returns `null` if header is missing or invalid
- Builds a standard `ClaimsPrincipal` with `ClaimsIdentity` from the decoded JSON
- Provides helper methods: `GetUserName()`, `GetUserEmail()`, `GetUserRoles()`

### Component 3: DashboardAuthMiddleware

**File**: `Auth/DashboardAuthMiddleware.cs` (new)
**Purpose**: ASP.NET Core middleware that intercepts dashboard route requests

Key behaviors:
- Only activates for requests matching the dashboard route prefix
- Only enforces auth when `RequireAuthentication` is `true`
- Returns 401 if no `X-MS-CLIENT-PRINCIPAL` header
- Returns 403 if user lacks required roles (when `AllowedRoles` is configured)
- Passes through all other requests unchanged

### Component 4: Service Registration

**File**: `ServiceCollectionExtensions.cs`
**Change**: Register the middleware in the pipeline when `RequireAuthentication` is enabled

### Component 5: Dashboard UI Updates

**File**: `wwwroot/dashboard.html`
**Changes**:
- Add user info area in the header (name + logout button)
- JavaScript calls `/.auth/me` on load to detect Easy Auth and get user info
- Handle 401/403 responses from API calls with a friendly auth-required message
- Add sign-out link pointing to `/.auth/logout`

### Component 6: Documentation

**File**: `README.md`
**Change**: Add authentication setup section referencing the quickstart guide

## Complexity Tracking

No constitution violations to justify. The implementation is straightforward:
- 2 new files (parser + middleware) with clear single responsibilities
- 2 modified files (options + service registration) with additive changes only
- 1 modified HTML file (dashboard UI)
- Zero new NuGet dependencies
