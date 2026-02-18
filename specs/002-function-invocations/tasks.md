# Tasks: Function Invocation History

**Input**: Design documents from `/specs/002-function-invocations/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Not requested in feature specification. No test tasks included.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No project initialization needed — project already exists with .NET 9.0, Azure.Identity, System.Text.Json, and Blazor Server. No new NuGet packages required.

*(No tasks in this phase)*

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, Application Insights client, and App ID discovery that MUST be complete before ANY user story can be implemented.

- [x] T001 [P] Create Application Insights response and domain entity models in AppInsightsModels.cs — include AppInsightsQueryResponse, AppInsightsTable, AppInsightsColumn, FunctionInvocation, and InvocationTrace classes per data-model.md
- [x] T002 [P] Add static helper method to extract ApplicationId from APPLICATIONINSIGHTS_CONNECTION_STRING env var in ContainerAppFunctionsClient.cs — parse semicolon-delimited connection string from containers[0].env
- [x] T003 Create AppInsightsClient.cs with constructor accepting TokenCredential, token acquisition for scope https://api.applicationinsights.io/.default, and GetInvocationsAsync method that POSTs KQL query to https://api.applicationinsights.io/v1/apps/{appId}/query per contracts/application-insights-api.md
- [x] T004 Register AppInsightsClient as singleton in Program.cs via builder.Services.AddSingleton<AppInsightsClient>()

**Checkpoint**: Foundation ready — Application Insights can be queried for invocation data. User story implementation can now begin.

---

## Phase 3: User Story 1 — View Invocation Summary Per Function (Priority: P1) MVP

**Goal**: Users click a function name in the list and see a detail page with recent invocations showing timestamp, success/failure status, and duration.

**Independent Test**: Click a function name in the list, verify the detail page loads with invocation rows showing timestamp, status badge, result code, and duration. Verify loading indicator while fetching. Verify empty state when no invocations exist.

### Implementation for User Story 1

- [x] T005 [US1] Create Components/Pages/FunctionDetail.razor with @page "/function/{FunctionName}" route, query parameters (sub, rg, app) via [SupplyParameterFromQuery], on-load call to extract App ID from container env vars then query invocations via AppInsightsClient.GetInvocationsAsync, display results in a table with columns: Timestamp, Status (success/fail badge), Result Code, Duration (ms), Invocation ID
- [x] T006 [US1] Add loading indicator, error display (including missing APPLICATIONINSIGHTS_CONNECTION_STRING message), and empty state ("No recent invocations found") to Components/Pages/FunctionDetail.razor
- [x] T007 [P] [US1] Update Components/Pages/Home.razor to make function names in the table clickable links that navigate to /function/{name}?sub={subscriptionId}&rg={resourceGroup}&app={appName}
- [x] T008 [P] [US1] Add styles to wwwroot/css/app.css for the function detail page: invocation table rows, success/failure status badges (green/red), duration formatting, and page header with function name

**Checkpoint**: User Story 1 is fully functional — users can drill into a function and see invocation history.

---

## Phase 4: User Story 2 — View Individual Invocation Traces (Priority: P2)

**Goal**: Users click an invocation row in the summary to expand inline trace logs showing timestamp, message, and severity level for that execution.

**Independent Test**: Click an invocation row, verify an inline panel expands below it showing trace log entries with timestamps, messages, and color-coded severity levels. Verify empty state when no traces found. Verify clicking again collapses the panel.

### Implementation for User Story 2

- [x] T009 [US2] Add GetInvocationTracesAsync method to AppInsightsClient.cs that queries traces table by operation_Id per contracts/application-insights-api.md, returns List<InvocationTrace>
- [x] T010 [US2] Add inline expandable trace panel to Components/Pages/FunctionDetail.razor — clicking an invocation row toggles a panel below it showing traces, with loading indicator while fetching and empty state for no traces
- [x] T011 [P] [US2] Add styles to wwwroot/css/app.css for trace panel: expandable row animation, severity level color coding (Verbose=gray, Information=blue, Warning=yellow, Error=red, Critical=purple), trace message formatting, timestamp column

**Checkpoint**: User Stories 1 AND 2 are functional — users can see invocations and drill into traces.

---

## Phase 5: User Story 3 — Navigate Back to Function List (Priority: P3)

**Goal**: Users can return from the function detail page to the function list with their previous search parameters (subscription, resource group, app name) preserved.

**Independent Test**: Navigate to a function detail page, click the back link, verify the Home page loads with the original subscription ID, resource group, and app name pre-filled in the form.

### Implementation for User Story 3

- [x] T012 [US3] Add back navigation link/button to Components/Pages/FunctionDetail.razor that links to /?sub={subscriptionId}&rg={resourceGroup}&app={appName} preserving query parameters
- [x] T013 [US3] Update Components/Pages/Home.razor to read sub, rg, and app query parameters via [SupplyParameterFromQuery] and pre-fill the search form fields on page load

**Checkpoint**: All user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Error handling edge cases and refinements across all stories.

- [x] T014 Add error handling to FunctionDetail.razor for edge cases: credentials lacking Application Insights read access (display permission error), Application Insights query timeouts (display retry message)
- [x] T015 Format duration values in FunctionDetail.razor invocation table — display as human-readable (e.g., "123 ms", "1.2 s", "1m 5s") instead of raw milliseconds

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Skipped — project already initialized
- **Foundational (Phase 2)**: No external dependencies — can start immediately
  - T001 and T002 can run in parallel (different files)
  - T003 depends on T001 (uses model types)
  - T004 depends on T003 (registers the client)
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion
  - T005 and T006 are sequential (same file)
  - T007 and T008 can run in parallel with T005/T006 (different files)
- **User Story 2 (Phase 4)**: Depends on Phase 3 completion (extends FunctionDetail.razor)
  - T009 can run in parallel with T010 prep (different files)
  - T010 depends on T009 (calls the new method)
  - T011 can run in parallel with T009/T010 (different file)
- **User Story 3 (Phase 5)**: Depends on Phase 3 completion (modifies Home.razor and FunctionDetail.razor)
  - T012 and T013 are independent (different files) but both should be done together
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Phase 2 — No dependencies on other stories
- **User Story 2 (P2)**: Can start after US1 — extends the FunctionDetail.razor page
- **User Story 3 (P3)**: Can start after US1 — modifies Home.razor and FunctionDetail.razor

### Parallel Opportunities

**Phase 2**:
```
Parallel: T001 (AppInsightsModels.cs) + T002 (ContainerAppFunctionsClient.cs)
Then: T003 (AppInsightsClient.cs)
Then: T004 (Program.cs)
```

**Phase 3 (US1)**:
```
Parallel: T005+T006 (FunctionDetail.razor) | T007 (Home.razor) | T008 (app.css)
```

**Phase 4 (US2)**:
```
Parallel: T009 (AppInsightsClient.cs) | T011 (app.css)
Then: T010 (FunctionDetail.razor)
```

**Phase 5 (US3)**:
```
Parallel: T012 (FunctionDetail.razor) | T013 (Home.razor)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: Foundational (T001-T004)
2. Complete Phase 3: User Story 1 (T005-T008)
3. **STOP and VALIDATE**: Click a function name, confirm invocations load
4. Deploy/demo if ready

### Incremental Delivery

1. Phase 2 → Foundation ready
2. Add User Story 1 (T005-T008) → Invocation summary works → Demo (MVP!)
3. Add User Story 2 (T009-T011) → Trace drill-down works → Demo
4. Add User Story 3 (T012-T013) → Back navigation works → Demo
5. Polish (T014-T015) → Edge cases handled → Final

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- No new NuGet packages needed — existing Azure.Identity and System.Text.Json are sufficient
- Two different token scopes: ARM uses `https://management.azure.com/.default`, Application Insights uses `https://api.applicationinsights.io/.default`
- KQL queries match the Azure CLI `az containerapp function invocations` implementation
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
