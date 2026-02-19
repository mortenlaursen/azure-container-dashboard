# Contract: Client Principal Parser

**Feature**: 001-entra-auth
**Date**: 2026-02-19

## Overview

An internal utility that parses the `X-MS-CLIENT-PRINCIPAL` header injected by Azure Container Apps Easy Auth into a structured object.

## Input

HTTP request header `X-MS-CLIENT-PRINCIPAL`: Base64-encoded JSON string.

## Parsing Steps

1. Check if the `X-MS-CLIENT-PRINCIPAL` header exists
2. Base64-decode the header value to a UTF-8 string
3. Deserialize the JSON into a `ClientPrincipal` object
4. Build a `ClaimsPrincipal` from the parsed claims

## Output: ClaimsPrincipal

The parser returns a standard .NET `ClaimsPrincipal` that can be used for:
- Checking `Identity.IsAuthenticated`
- Reading `Identity.Name`
- Checking roles via `IsInRole(roleName)`
- Accessing any claim via `FindFirst(claimType)`

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Header missing | Return null |
| Header is not valid Base64 | Return null (log warning) |
| JSON deserialization fails | Return null (log warning) |
| Claims array is empty | Return ClaimsPrincipal with empty claims |

## Usage

The middleware calls the parser for every request to a dashboard route when `RequireAuthentication` is true. The parser result determines whether the request is allowed or rejected.

## Helper Methods

The parser also provides convenience methods:

| Method | Returns | Description |
|--------|---------|-------------|
| GetUserName | string? | Extracts display name from claims |
| GetUserEmail | string? | Extracts email from claims |
| GetUserRoles | string[] | Extracts all role claims |
| HasRole(roleName) | bool | Checks if user has a specific role |
