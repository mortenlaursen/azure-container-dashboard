# Tasks: Start and Stop Functions

**Input**: Design documents from `/specs/001-start-stop-functions/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: No test tasks included — no test framework is configured and tests were not requested.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Foundational (Blocking Prerequisites)

**Purpose**: Add the Container App models, client methods, and CSS styles required by ALL user stories

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T001 [P] Add Container App partial models (ContainerApp, ContainerAppProperties, ContainerAppTemplate, ContainerAppContainer, EnvironmentVariable) with [JsonPropertyName] attributes to ContainerAppFunction.cs — use JsonElement for ContainerAppContainer.Resources to preserve unknown fields during GET/PATCH round-trip; mark all nullable fields with `?`; see data-model.md for full field definitions
- [x] T002 [P] Add action button and state CSS styles to wwwroot/css/app.css — add .btn-danger (red, matches .badge-disabled colors), .btn-success (green, matches .badge-active colors), .btn-sm (small padding for table cells), .btn-confirm/.btn-cancel (inline confirmation), .actions-cell (flex layout), .row-loading (opacity/animation), .success-box (green, mirrors .error-box pattern)
- [x] T003 Add GetContainerAppAsync method to ContainerAppFunctionsClient.cs — GET /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.App/containerApps/{app}?api-version=2025-10-02-preview; returns deserialized ContainerApp; reuse existing SendRequestAsync pattern
- [x] T004 Add SendPatchRequestAsync private method to ContainerAppFunctionsClient.cs — accepts URL and object body; serializes body with JsonIgnoreCondition.WhenWritingNull; sends PATCH with Content-Type application/merge-patch+json and Bearer token; returns deserialized response or throws HttpRequestException with status and body on failure
- [x] T005 Add SetFunctionDisabledAsync public method to ContainerAppFunctionsClient.cs — accepts subscriptionId, resourceGroupName, containerAppName, functionName, disabled bool, cancellationToken; calls GetContainerAppAsync to read current state; finds or adds AzureWebJobs.{functionName}.Disabled env var on first container; sets value to "true" or "false"; calls SendPatchRequestAsync with modified ContainerApp; see contracts/azure-arm-api.md for full PATCH body structure

**Checkpoint**: Foundation ready — the client can now GET Container Apps and PATCH environment variables to enable/disable functions

---

## Phase 2: User Story 1 — Stop a Running Function (Priority: P1) 🎯 MVP

**Goal**: Allow operators to stop (disable) an active function from the function list with a confirmation step

**Independent Test**: List functions with at least one active function → click Stop → confirm → verify status changes to Disabled and success message appears. Cancel the confirmation and verify no change occurs.

### Implementation for User Story 1

- [x] T006 [US1] Add Actions column to the function table in Components/Pages/Home.razor — add `<th>Actions</th>` to thead and a new `<td class="actions-cell">` in the foreach loop; inside the td, show a Stop button (`<button class="btn-danger btn-sm">Stop</button>`) only when `!func.Properties.IsDisabled`; wire onclick to `RequestStop(func.Name)`
- [x] T007 [US1] Add inline confirmation for stop actions in Components/Pages/Home.razor — add `HashSet<string> confirmingStop = new()` to @code; implement `RequestStop(name)` that adds name to confirmingStop and calls StateHasChanged; implement `CancelStop(name)` that removes from confirmingStop; in the Actions td, when `confirmingStop.Contains(func.Name)`, replace the Stop button with "Are you sure?" text plus Confirm (`btn-danger btn-sm`) and Cancel (`btn-sm`) buttons
- [x] T008 [US1] Implement ConfirmStop method in Components/Pages/Home.razor — `async Task ConfirmStop(string functionName)`: remove from confirmingStop; call `Client.SetFunctionDisabledAsync(subscriptionId, resourceGroup, appName, functionName, disabled: true)`; on success set `func.Properties.IsDisabled = true` and show success message; on failure set error message; wrap in try/catch for HttpRequestException
- [x] T009 [US1] Add success message display in Components/Pages/Home.razor — add `string? successMessage` field to @code; display `<div class="success-box">@successMessage</div>` below the error-box when not null; clear successMessage when starting a new operation or loading functions; auto-clear after displaying

**Checkpoint**: User Story 1 is complete — operators can stop active functions with confirmation. This is the MVP.

---

## Phase 3: User Story 2 — Start a Stopped Function (Priority: P2)

**Goal**: Allow operators to start (enable) a disabled function from the function list without requiring confirmation

**Independent Test**: List functions with at least one disabled function → click Start → verify status changes to Active and success message appears.

### Implementation for User Story 2

- [x] T010 [US2] Add Start button for disabled functions in Components/Pages/Home.razor — in the Actions td, when `func.Properties.IsDisabled` and not confirming stop, show a Start button (`<button class="btn-success btn-sm">Start</button>`); wire onclick to `StartFunction(func.Name)`
- [x] T011 [US2] Implement StartFunction method in Components/Pages/Home.razor — `async Task StartFunction(string functionName)`: call `Client.SetFunctionDisabledAsync(subscriptionId, resourceGroup, appName, functionName, disabled: false)`; on success set `func.Properties.IsDisabled = false` and show success message; on failure set error message; wrap in try/catch for HttpRequestException

**Checkpoint**: User Stories 1 AND 2 are complete — operators can both stop and start functions.

---

## Phase 4: User Story 3 — Operation Feedback During Start/Stop (Priority: P3)

**Goal**: Show visual loading feedback during operations and prevent duplicate actions

**Independent Test**: Trigger a start or stop action → verify loading indicator appears on the row → verify the action button is disabled during the operation → verify indicator clears after completion or failure.

### Implementation for User Story 3

- [x] T012 [US3] Add per-row loading state tracking in Components/Pages/Home.razor — add `HashSet<string> operationsInProgress = new()` to @code; in ConfirmStop and StartFunction, add function name to operationsInProgress before the API call and remove it in the finally block; conditionally add `row-loading` CSS class to `<tr>` when function name is in operationsInProgress
- [x] T013 [US3] Disable action buttons during in-progress operations in Components/Pages/Home.razor — add `disabled="@operationsInProgress.Contains(func.Name)"` to Start, Stop, Confirm, and Cancel buttons; when an operation is in progress for a function, show "Working..." text instead of the action buttons

**Checkpoint**: All three user stories are complete and independently testable.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Verify everything works together and clean up

- [x] T014 Verify the project builds without errors by running `dotnet build` from the repository root
- [x] T015 Review all modified files for consistency — ensure all new models use [JsonPropertyName], all new CSS classes follow existing naming conventions, all error messages are user-friendly per FR-008

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — can start immediately. T001 and T002 can run in parallel (different files). T003 depends on T001. T004 is independent of T001. T005 depends on T003 and T004.
- **User Story 1 (Phase 2)**: Depends on Phase 1 completion (T005). T006 → T007 → T008 → T009 (sequential, same file).
- **User Story 2 (Phase 3)**: Depends on Phase 2 completion (modifies same file). T010 → T011 (sequential, same file).
- **User Story 3 (Phase 4)**: Depends on Phase 3 completion (modifies same file). T012 → T013 (sequential, same file).
- **Polish (Phase 5)**: Depends on all user stories being complete.

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational phase — no dependencies on other stories
- **User Story 2 (P2)**: Builds on US1 (shares the Actions column and success message display), but is independently testable
- **User Story 3 (P3)**: Builds on US1 and US2 (adds loading states to existing start/stop methods), but is independently testable

### Within Each User Story

- All US tasks are sequential (same file: Home.razor)
- Models → client methods → UI → styles (cross-file dependency order)

### Parallel Opportunities

- **Phase 1**: T001 (models) and T002 (CSS) can run in parallel — different files
- **Phase 1**: T003 and T004 can run in parallel after T001 — different methods, no interdependency
- **Cross-phase**: CSS work (T002) is independent of all other phases and can be done at any time

---

## Parallel Example: Foundational Phase

```text
# Launch models and CSS in parallel:
Task: "Add Container App models in ContainerAppFunction.cs" (T001)
Task: "Add CSS styles in wwwroot/css/app.css" (T002)

# After T001 completes, launch client methods in parallel:
Task: "Add GetContainerAppAsync in ContainerAppFunctionsClient.cs" (T003)
Task: "Add SendPatchRequestAsync in ContainerAppFunctionsClient.cs" (T004)

# After T003+T004 complete:
Task: "Add SetFunctionDisabledAsync in ContainerAppFunctionsClient.cs" (T005)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Foundational (T001-T005)
2. Complete Phase 2: User Story 1 — Stop a Function (T006-T009)
3. **STOP and VALIDATE**: Test stopping an active function with confirmation
4. Deploy/demo if ready — operators can stop misbehaving functions

### Incremental Delivery

1. Foundational → Foundation ready
2. Add User Story 1 → Test independently → Deploy/Demo (**MVP!** — stop functions)
3. Add User Story 2 → Test independently → Deploy/Demo (start + stop functions)
4. Add User Story 3 → Test independently → Deploy/Demo (full polish with loading states)
5. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- All user story tasks modify Components/Pages/Home.razor — they MUST be sequential
- Commit after each phase completion for clean git history
- The project has no test framework — validation is manual against a live Azure subscription
- Environment variable changes create a new Container App revision (10-30s propagation)
