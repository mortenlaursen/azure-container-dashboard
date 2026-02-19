# Contract: Dashboard Authentication Middleware

**Feature**: 001-entra-auth
**Date**: 2026-02-19

## Overview

An ASP.NET Core middleware that intercepts all requests to dashboard routes (`/dashboard/**`) and enforces authentication when `RequireAuthentication` is enabled in `DashboardOptions`.

## Behavior

### Route Matching

The middleware applies ONLY to routes starting with the configured route prefix (default: `dashboard`). All other routes in the Functions app are unaffected.

### Request Flow

```
Request to /dashboard/*
    │
    ├─ DashboardOptions.RequireAuthentication == false
    │   └─ Pass through to next middleware (no auth check)
    │
    └─ DashboardOptions.RequireAuthentication == true
        │
        ├─ "X-MS-CLIENT-PRINCIPAL" header missing
        │   └─ Return HTTP 401 Unauthorized
        │       Body: { "error": "Authentication required. Please configure Easy Auth on your Container App." }
        │
        ├─ Header present, AllowedRoles is empty
        │   └─ Pass through (any authenticated user allowed)
        │
        ├─ Header present, user has at least one role in AllowedRoles
        │   └─ Pass through
        │
        └─ Header present, user has no matching roles
            └─ Return HTTP 403 Forbidden
                Body: { "error": "Access denied. You do not have the required role to access this dashboard." }
```

### Error Responses

| Status | Condition | Response Body |
|--------|-----------|---------------|
| 401 | No `X-MS-CLIENT-PRINCIPAL` header when auth required | `{ "error": "Authentication required. Please configure Easy Auth on your Container App." }` |
| 403 | Authenticated but lacks required role | `{ "error": "Access denied. You do not have the required role to access this dashboard." }` |

## Configuration API

```csharp
// Minimal setup — require authentication, allow any tenant user
services.AddContainerDashboard(options =>
{
    options.RequireAuthentication = true;
});

// With role restriction
services.AddContainerDashboard(options =>
{
    options.RequireAuthentication = true;
    options.AllowedRoles = ["Dashboard.Admin", "Dashboard.Reader"];
});
```
