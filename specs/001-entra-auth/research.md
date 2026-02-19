# Research: Entra ID Authentication for Dashboard

**Feature**: 001-entra-auth
**Date**: 2026-02-19

## Decision 1: Authentication Approach

**Decision**: Use Azure Container Apps Built-in Authentication (Easy Auth) as the primary mechanism, with optional in-app header parsing for user display and authorization enforcement.

**Rationale**:
- Easy Auth runs as a sidecar container on Container Apps — zero application code needed for basic protection
- Handles the entire OAuth2 redirect flow, cookie management, and session lifecycle
- Platform strips and re-injects `X-MS-CLIENT-PRINCIPAL` headers, so they cannot be spoofed by external requests
- The dashboard is a browser-based UI, which is the ideal use case for Easy Auth's server-directed sign-in flow
- No additional NuGet dependencies required for the core auth flow
- Officially recommended by Microsoft for securing Azure Functions on Container Apps

**Alternatives considered**:
- **Microsoft.Identity.Web (in-code JWT validation)**: Rejected. Adds package dependencies, requires app-managed OIDC flow, cannot be combined with Easy Auth without conflicts, and is more complex for browser-based UIs. Better suited for pure API scenarios with bearer tokens.
- **Azure Functions access keys (AuthorizationLevel.Function)**: Not supported on Container Apps. Even if it were, it's key-based, not identity-based — doesn't meet the "who is accessing" requirement.
- **Custom OIDC middleware**: Over-engineered for this use case. Easy Auth provides the same functionality with zero code.

## Decision 2: In-App Auth Enforcement

**Decision**: Add optional middleware in the NuGet package that validates the `X-MS-CLIENT-PRINCIPAL` header on all dashboard routes when auth is enabled in configuration.

**Rationale**:
- Provides defense-in-depth: even if Easy Auth is misconfigured or bypassed, the app itself rejects unauthenticated requests
- Allows the package to offer a "require authentication" option that developers can enable
- Reads the standard Easy Auth headers — no coupling to a specific identity provider
- Minimal code: just check for header presence and optionally validate tenant/role claims

**Alternatives considered**:
- **No in-app enforcement (rely entirely on Easy Auth)**: Rejected. Users might misconfigure Easy Auth (e.g., set to AllowAnonymous by mistake), leaving the dashboard exposed. Defense-in-depth is the safer default.
- **Full JWT validation in-app**: Rejected. Would duplicate what Easy Auth already does and add complexity/dependencies.

## Decision 3: User Display in Dashboard UI

**Decision**: Add user identity display (name, logout button) to the dashboard HTML when authentication is detected.

**Rationale**:
- The `X-MS-CLIENT-PRINCIPAL-NAME` header provides the user's display name without any decoding
- Easy Auth exposes `/.auth/logout` for sign-out — just needs a link
- JavaScript can call `/.auth/me` to get user details for richer display
- Improves UX by showing who is signed in and providing a way to sign out

**Alternatives considered**:
- **No UI changes**: Rejected. Users would have no indication they're authenticated or how to sign out.
- **Full user profile page**: Rejected. Over-engineered for a dashboard tool.

## Decision 4: Configuration API Design

**Decision**: Extend `DashboardOptions` with authentication properties and keep the existing `AddContainerDashboard()` pattern.

**Rationale**:
- Follows the existing configuration pattern used for subscription, resource group, and app name
- Keeps the single-method setup experience (`AddContainerDashboard(options => { ... })`)
- No new extension methods or service registrations needed
- Backward compatible: auth is off by default

**Alternatives considered**:
- **Separate `AddContainerDashboardAuth()` method**: Rejected. Splits configuration across two calls unnecessarily.
- **Separate options class**: Rejected. Adds complexity for a few boolean/string properties.

## Decision 5: Allowed Roles/Groups

**Decision**: Support optional role-based filtering via configuration. If specified, only users with matching roles in their `X-MS-CLIENT-PRINCIPAL` claims can access the dashboard.

**Rationale**:
- Easy Auth passes role claims when the Entra ID app registration is configured with app roles
- Some organizations want to restrict dashboard access to specific teams (e.g., "Dashboard.Admin" role)
- Optional — if not configured, any authenticated user in the tenant has access

**Alternatives considered**:
- **No role filtering**: Considered but kept as optional. Tenant-level access is the default, roles are opt-in.
- **Group-based filtering**: Groups require additional Graph API calls. Roles are simpler and available directly in claims.

## Technical Findings

### Easy Auth Header Format

The `X-MS-CLIENT-PRINCIPAL` header is Base64-encoded JSON:

```json
{
  "auth_typ": "aad",
  "name_typ": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
  "role_typ": "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
  "claims": [
    { "typ": "name", "val": "John Doe" },
    { "typ": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "val": "john@contoso.com" },
    { "typ": "roles", "val": "Dashboard.Admin" }
  ]
}
```

### Easy Auth Endpoints

| Endpoint | Purpose |
|----------|---------|
| `/.auth/login/aad` | Redirect to Entra ID sign-in |
| `/.auth/login/aad/callback` | OAuth callback (used by platform) |
| `/.auth/logout` | Sign out and clear session |
| `/.auth/me` | Return authenticated user info (JSON) |

### Key Constraints

- `Microsoft.Identity.Web` does NOT work with Container Apps Easy Auth — they conflict
- `.NET ClaimsPrincipal` is NOT automatically populated by Easy Auth on Container Apps — must parse headers manually
- Easy Auth cannot be tested locally — the sidecar only runs in Azure
- The `X-MS-CLIENT-PRINCIPAL` header is stripped and re-injected by the sidecar — cannot be spoofed from external requests

### Sources

- https://learn.microsoft.com/en-us/azure/container-apps/authentication
- https://learn.microsoft.com/en-us/azure/container-apps/authentication-entra
- https://learn.microsoft.com/en-us/azure/app-service/configure-authentication-user-identities
- https://learn.microsoft.com/en-us/azure/container-apps/functions-overview
