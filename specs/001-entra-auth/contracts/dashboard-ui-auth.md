# Contract: Dashboard UI Authentication

**Feature**: 001-entra-auth
**Date**: 2026-02-19

## Overview

Changes to the `dashboard.html` frontend to support user identity display and sign-out when Easy Auth is active.

## Detection

The dashboard JavaScript detects Easy Auth by calling `/.auth/me`:
- If the endpoint returns user data → Easy Auth is active, show user info
- If the endpoint returns an error or empty → Easy Auth is not configured, hide auth UI elements

## UI Elements

### User Info Area (top-right of header)

When Easy Auth is detected:
```
┌──────────────────────────────────────────────────────┐
│  Container App Dashboard              user@org.com ▼ │
│                                       ┌────────────┐ │
│                                       │ Sign out   │ │
│                                       └────────────┘ │
└──────────────────────────────────────────────────────┘
```

- Display the user's name or email from `/.auth/me` response
- Dropdown or button linking to `/.auth/logout?post_logout_redirect_uri=/dashboard`

When Easy Auth is NOT detected:
- No user info area shown (current behavior preserved)

### 401/403 Error Handling

When dashboard API calls return 401 or 403:
- Show a message: "You are not authorized to access this dashboard. Please sign in."
- Provide a link to `/.auth/login/aad?post_login_redirect_uri=/dashboard`

## API Contract: /.auth/me

This is a platform-provided endpoint (not implemented by the package). Expected response format:

```json
[{
    "user_id": "user@contoso.com",
    "user_claims": [
        { "typ": "name", "val": "John Doe" },
        { "typ": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "val": "user@contoso.com" }
    ]
}]
```

The dashboard reads:
- `user_claims` where `typ` contains `emailaddress` or `name` → display in header
- Falls back to `user_id` if no name claim found

## Behavior Matrix

| Easy Auth Active | RequireAuth | UI Behavior |
|-----------------|-------------|-------------|
| No | false | Dashboard works as today, no auth UI |
| No | true | All API calls return 401, dashboard shows auth required message |
| Yes | false | Dashboard works, shows user info + logout if user is signed in |
| Yes | true | Dashboard shows user info + logout, unauthorized users see 401/403 |
