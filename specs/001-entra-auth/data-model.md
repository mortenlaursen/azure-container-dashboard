# Data Model: Entra ID Authentication

**Feature**: 001-entra-auth
**Date**: 2026-02-19

## Entities

### DashboardAuthOptions (new properties on existing DashboardOptions)

Configuration properties added to the existing `DashboardOptions` class.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| RequireAuthentication | boolean | false | When true, all dashboard routes require a valid Easy Auth identity |
| AllowedRoles | list of strings | empty (all roles) | If non-empty, only users with at least one matching role claim can access the dashboard |

**Validation Rules**:
- `RequireAuthentication` must be explicitly set to `true` to enable auth enforcement
- `AllowedRoles` is only evaluated when `RequireAuthentication` is `true`
- An empty `AllowedRoles` list means any authenticated user is allowed (tenant-level access)

### ClientPrincipal (parsed from Easy Auth header)

Represents a decoded `X-MS-CLIENT-PRINCIPAL` header. Internal model — not exposed to package consumers.

| Field | Type | Source | Description |
|-------|------|--------|-------------|
| IdentityProvider | string | `auth_typ` | The identity provider (e.g., "aad") |
| NameClaimType | string | `name_typ` | The claim type used for the user's name |
| RoleClaimType | string | `role_typ` | The claim type used for roles |
| Claims | list of Claim | `claims` | All claims from the identity provider |

### ClientPrincipalClaim (element within ClientPrincipal)

| Field | Type | Source | Description |
|-------|------|--------|-------------|
| Type | string | `typ` | The claim type URI |
| Value | string | `val` | The claim value |

### DashboardUser (derived from ClientPrincipal)

Represents the authenticated user for display purposes. Extracted from claims.

| Field | Type | Source Claim | Description |
|-------|------|-------------|-------------|
| Name | string | `name` | User's display name |
| Email | string | `emailaddress` claim | User's email address |
| Roles | list of strings | `roles` claims | Roles assigned to the user |

## Relationships

```
DashboardOptions (1) ──contains──> DashboardAuthOptions (properties)
                                        │
                                        │ enforces via middleware
                                        ▼
HttpRequest ──header──> X-MS-CLIENT-PRINCIPAL ──parsed to──> ClientPrincipal
                                                                │
                                                                │ extracted to
                                                                ▼
                                                          DashboardUser (for UI display)
```

## State Transitions

### Authentication Flow (per request)

```
[Request arrives]
    │
    ├─ RequireAuthentication = false → [Allow through] → Dashboard renders normally
    │
    └─ RequireAuthentication = true
         │
         ├─ X-MS-CLIENT-PRINCIPAL header missing → [Return 401]
         │     (Easy Auth handles redirect if configured)
         │
         └─ Header present → [Parse ClientPrincipal]
              │
              ├─ AllowedRoles is empty → [Allow through] → Dashboard renders with user info
              │
              └─ AllowedRoles is non-empty
                   │
                   ├─ User has matching role → [Allow through] → Dashboard renders with user info
                   │
                   └─ User lacks matching role → [Return 403 Access Denied]
```
