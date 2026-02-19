# Tasks: Entra ID Authentication for Dashboard

**Input**: Design documents from `/specs/001-entra-auth/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Included — plan.md explicitly specifies xUnit tests for middleware and parser code.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create project structure for new auth components and test project

- [X] T001 Create `Auth/` directory under `src/Azure.Container.Dashboard.Functions/`
- [X] T002 [P] Create xUnit test project `test/Azure.Container.Dashboard.Tests/Azure.Container.Dashboard.Tests.csproj` with project reference to `src/Azure.Container.Dashboard.Functions/Azure.Container.Dashboard.Functions.csproj` and `Auth/` test directory

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models and configuration that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T003 Add `RequireAuthentication` (bool, default `false`) and `AllowedRoles` (List\<string\>, default `[]`) properties to `src/Azure.Container.Dashboard.Functions/DashboardOptions.cs` — must be backward compatible with existing deployments
- [X] T004 [P] Implement `ClientPrincipalParser` static class with `ClientPrincipal` and `ClientPrincipalClaim` internal models in `src/Azure.Container.Dashboard.Functions/Auth/ClientPrincipalParser.cs` — must include: `Parse(HttpRequest)` returning `ClaimsPrincipal?`, `GetUserName(ClaimsPrincipal)`, `GetUserEmail(ClaimsPrincipal)`, `GetUserRoles(ClaimsPrincipal)`, `HasRole(ClaimsPrincipal, string)` helper methods; return `null` when header is missing or malformed (per contracts/client-principal-parser.md); parse Base64-encoded `X-MS-CLIENT-PRINCIPAL` header into `ClaimsIdentity` using `auth_typ`, `name_typ`, `role_typ`, and `claims` fields from the JSON payload (see research.md for header format)

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 — Protect Dashboard Behind Authentication (Priority: P1) 🎯 MVP

**Goal**: All dashboard routes are protected when `RequireAuthentication` is enabled. Unauthenticated requests return 401, unauthorized users (missing required role) return 403.

**Independent Test**: Navigate to `/dashboard` without `X-MS-CLIENT-PRINCIPAL` header when `RequireAuthentication = true` and verify 401 is returned. Then add the header and verify access is granted. Then configure `AllowedRoles` and verify 403 when role is missing.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T005 [P] [US1] Write unit tests for `ClientPrincipalParser` in `test/Azure.Container.Dashboard.Tests/Auth/ClientPrincipalParserTests.cs` — test cases: missing header returns null, invalid Base64 returns null, invalid JSON returns null, valid header returns ClaimsPrincipal with correct identity/claims, empty claims array returns principal with empty claims, helper methods (GetUserName, GetUserEmail, GetUserRoles, HasRole) return correct values
- [X] T006 [P] [US1] Write unit tests for `DashboardAuthMiddleware` in `test/Azure.Container.Dashboard.Tests/Auth/DashboardAuthMiddlewareTests.cs` — test cases: non-dashboard route passes through unchanged, dashboard route with RequireAuthentication=false passes through, dashboard route with RequireAuthentication=true and no header returns 401 with JSON error body, dashboard route with valid header and empty AllowedRoles passes through, dashboard route with valid header and matching role passes through, dashboard route with valid header and non-matching role returns 403 with JSON error body

### Implementation for User Story 1

- [X] T007 [US1] Implement `DashboardAuthMiddleware` as ASP.NET Core middleware in `src/Azure.Container.Dashboard.Functions/Auth/DashboardAuthMiddleware.cs` — inject `IOptions<DashboardOptions>`, only intercept requests matching the dashboard route prefix, skip auth check when `RequireAuthentication` is false, return 401 JSON when `X-MS-CLIENT-PRINCIPAL` header is missing, return 403 JSON when `AllowedRoles` is non-empty and user lacks matching role, pass through otherwise (per contracts/auth-middleware.md)
- [X] T008 [US1] Register `DashboardAuthMiddleware` in the ASP.NET Core pipeline in `src/Azure.Container.Dashboard.Functions/ServiceCollectionExtensions.cs` — conditionally add middleware when `RequireAuthentication` is `true`; ensure existing `AddContainerDashboard` pattern is preserved

**Checkpoint**: At this point, User Story 1 should be fully functional and testable independently. All dashboard routes are protected via middleware when auth is enabled.

---

## Phase 4: User Story 2 — Simple Setup Experience (Priority: P1)

**Goal**: Developers can enable authentication with a single configuration change and complete setup in under 10 minutes following documentation. Existing deployments without auth config continue to work unchanged.

**Independent Test**: Add `RequireAuthentication = true` to a new project using `AddContainerDashboard`, verify dashboard routes are protected. Remove the setting, verify dashboard works as before.

### Implementation for User Story 2

- [X] T009 [US2] Verify backward compatibility — confirm that the default values (`RequireAuthentication = false`, `AllowedRoles = []`) result in zero behavioral changes for existing deployments; ensure no new required configuration, no new NuGet dependencies, and no changes to the `.csproj` file `src/Azure.Container.Dashboard.Functions/Azure.Container.Dashboard.Functions.csproj`
- [X] T010 [US2] Add authentication setup section to `README.md` referencing the quickstart guide at `specs/001-entra-auth/quickstart.md` — include: enabling `RequireAuthentication` in `AddContainerDashboard` options, setting up Easy Auth on Container App, and optional `AllowedRoles` configuration

**Checkpoint**: At this point, User Stories 1 AND 2 should both work independently. Developers can enable auth with minimal config.

---

## Phase 5: User Story 3 — Seamless Experience for Authorized Users (Priority: P2)

**Goal**: Authenticated users see their identity in the dashboard header with a sign-out option. The dashboard gracefully handles 401/403 errors from API calls with friendly messages and sign-in links.

**Independent Test**: Sign in via Easy Auth, navigate to `/dashboard`, verify user name/email appears in the header and a sign-out button is present. Click sign-out and verify redirect to `/.auth/logout`. Test with a user who lacks required roles and verify friendly "Access Denied" message with sign-in link.

### Implementation for User Story 3

- [X] T011 [P] [US3] Add user authentication UI elements to `src/Azure.Container.Dashboard.Functions/wwwroot/dashboard.html` — JavaScript calls `/.auth/me` on page load to detect Easy Auth; if active, display user name/email in top-right header area with a sign-out dropdown/button linking to `/.auth/logout?post_logout_redirect_uri=/dashboard`; if not active, hide auth UI elements (preserve current behavior); handle 401/403 API responses by showing "You are not authorized to access this dashboard. Please sign in." message with link to `/.auth/login/aad?post_login_redirect_uri=/dashboard` (per contracts/dashboard-ui-auth.md)
- [X] T012 [P] [US3] Pass authenticated user info to HTML response in `src/Azure.Container.Dashboard.Functions/Functions/DashboardUiFunction.cs` — read `X-MS-CLIENT-PRINCIPAL-NAME` header and inject user display name into the dashboard HTML response for server-side rendering; if header is absent, render dashboard without user info (current behavior)

**Checkpoint**: All user stories should now be independently functional. Dashboard shows user info when authenticated and handles errors gracefully.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validation and improvements that affect multiple user stories

- [X] T013 [P] Run quickstart.md validation — follow the steps in `specs/001-entra-auth/quickstart.md` end-to-end to verify the setup flow works as documented
- [X] T014 Code review and security check across all modified and new files — verify no secrets/credentials in code, JSON error responses use correct content types, Base64 parsing handles edge cases safely, no XSS vectors in user info display in dashboard.html

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational (T003, T004) — core auth enforcement
- **US2 (Phase 4)**: Depends on US1 completion — validates setup experience
- **US3 (Phase 5)**: Depends on Foundational (T003, T004) — can run in parallel with US1/US2
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — No dependencies on other stories
- **User Story 2 (P1)**: Depends on US1 being complete (need working auth to validate setup experience)
- **User Story 3 (P2)**: Can start after Foundational (Phase 2) — Independent of US1/US2 (UI changes only)

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models before services
- Services/middleware before registration
- Core implementation before integration
- Story complete before moving to next priority

### Parallel Opportunities

- T001 and T002 can run in parallel (different directories)
- T003 and T004 can run in parallel (different files)
- T005 and T006 can run in parallel (different test files, both written before implementation)
- T011 and T012 can run in parallel (different files: dashboard.html vs DashboardUiFunction.cs)
- T013 and T014 can run in parallel (independent validation tasks)
- US1 and US3 can be worked on in parallel after Foundational completes (independent concerns)

---

## Parallel Example: User Story 1

```bash
# Launch tests for User Story 1 together (write before implementation):
Task: "Unit tests for ClientPrincipalParser in test/Azure.Container.Dashboard.Tests/Auth/ClientPrincipalParserTests.cs"
Task: "Unit tests for DashboardAuthMiddleware in test/Azure.Container.Dashboard.Tests/Auth/DashboardAuthMiddlewareTests.cs"

# Then implement sequentially (middleware depends on parser):
Task: "Implement DashboardAuthMiddleware in src/Azure.Container.Dashboard.Functions/Auth/DashboardAuthMiddleware.cs"
Task: "Register middleware in src/Azure.Container.Dashboard.Functions/ServiceCollectionExtensions.cs"
```

## Parallel Example: User Story 3

```bash
# Both UI tasks can run in parallel (different files):
Task: "Add auth UI elements to src/Azure.Container.Dashboard.Functions/wwwroot/dashboard.html"
Task: "Pass user info in src/Azure.Container.Dashboard.Functions/Functions/DashboardUiFunction.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational (T003–T004)
3. Complete Phase 3: User Story 1 (T005–T008)
4. **STOP and VALIDATE**: Test auth enforcement end-to-end
5. Deploy/demo if ready — dashboard is now protected

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!)
3. Add User Story 2 → Validate setup experience + docs → Deploy/Demo
4. Add User Story 3 → Test user info display + error handling → Deploy/Demo
5. Each story adds value without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (auth enforcement)
   - Developer B: User Story 3 (UI changes — independent)
3. After US1 completes: Developer A picks up User Story 2 (docs + validation)
4. Stories complete and integrate independently

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Zero new NuGet dependencies — uses only System.Text.Json (already referenced) and standard .NET APIs
- All error responses use JSON format per contracts/auth-middleware.md
