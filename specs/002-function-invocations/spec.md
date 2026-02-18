# Feature Specification: Function Invocation History

**Feature Branch**: `002-function-invocations`
**Created**: 2026-02-17
**Status**: Draft
**Input**: User description: "In the old azure UI for function you could see invocation. Could you make something that is similar pr function. maybe when you can drill into a function and see it"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Invocation Summary Per Function (Priority: P1)

A user viewing the function list wants to drill into an individual function to see a summary of recent invocations. They click on a function name in the table and are taken to a detail view showing invocation history: when each invocation ran, whether it succeeded or failed, and how long it took. This mirrors the "Monitor" tab from the classic Azure Functions portal.

**Why this priority**: This is the core value of the feature. Without seeing invocation history, there is nothing to drill into. This delivers immediate operational visibility per function.

**Independent Test**: Can be fully tested by clicking a function name in the list, viewing the invocation summary table, and confirming it shows recent invocations with timestamps, status, and duration.

**Acceptance Scenarios**:

1. **Given** the user is on the functions list page, **When** they click a function name, **Then** they navigate to a detail view for that function showing recent invocations.
2. **Given** the function detail view is displayed, **When** invocations exist, **Then** a table shows each invocation with: timestamp, status (success/failure), and duration.
3. **Given** the function detail view is displayed, **When** no invocations are found, **Then** a clear empty state message is shown indicating no recent invocations.
4. **Given** the function detail view is displayed, **When** invocations are loading, **Then** a loading indicator is shown.

---

### User Story 2 - View Individual Invocation Traces (Priority: P2)

From the invocation summary, the user wants to drill further into a single invocation to see its trace logs. They click on an invocation row and see the detailed trace output for that execution — log messages, timestamps, and severity levels. This helps diagnose why a specific invocation failed or ran slowly.

**Why this priority**: Traces provide the debugging depth needed to act on what the summary reveals. Without traces, users can see that something failed but not why.

**Independent Test**: Can be tested by clicking an invocation row in the summary table and verifying that trace logs for that specific invocation are displayed with timestamps and log levels.

**Acceptance Scenarios**:

1. **Given** the user is on the invocation summary, **When** they click an invocation row, **Then** the trace details for that invocation are displayed.
2. **Given** trace details are displayed, **When** traces exist, **Then** each trace shows timestamp, message, and severity level.
3. **Given** trace details are displayed, **When** no traces are found for an invocation, **Then** a message indicates no trace data is available.

---

### User Story 3 - Navigate Back to Function List (Priority: P3)

The user viewing a function detail or invocation traces wants to easily navigate back to the function list without losing their original search context (subscription, resource group, app name).

**Why this priority**: Smooth navigation is essential for usability but is less critical than the data display itself.

**Independent Test**: Can be tested by navigating into a function detail, then clicking back, and confirming the original function list is displayed with the same search parameters.

**Acceptance Scenarios**:

1. **Given** the user is on the function detail view, **When** they click a back/return link, **Then** they return to the function list with their previous search parameters preserved.

---

### Edge Cases

- What happens when Application Insights is not configured for the container app? The system should display a clear message indicating that invocation data requires Application Insights to be connected.
- What happens when the user's credentials lack read access to Application Insights? An appropriate permission error should be shown.
- What happens when there are thousands of invocations? The summary should display a reasonable default page size (e.g., 50 most recent) without overwhelming the user.
- How does the system handle invocations that are still in progress? They should appear with a "Running" status if the data source reports them.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow users to click on a function name in the list to navigate to a function detail view.
- **FR-002**: System MUST display a summary of recent invocations for the selected function, including timestamp, success/failure status, and duration.
- **FR-003**: System MUST allow users to click on an individual invocation to view its trace logs.
- **FR-004**: System MUST display trace logs with timestamp, message text, and severity level for each selected invocation.
- **FR-005**: System MUST show a clear empty state when no invocation data is available for a function.
- **FR-006**: System MUST show an informative error when invocation data cannot be retrieved (e.g., Application Insights not configured, permission issues).
- **FR-007**: System MUST provide navigation back to the function list from the detail view, preserving the user's previous search parameters.
- **FR-008**: System MUST display the most recent invocations first (newest at top).
- **FR-009**: System MUST show a loading indicator while invocation data is being fetched.

### Key Entities

- **Function Invocation**: A single execution of a function; key attributes include invocation ID, function name, timestamp, status (success/failure), and duration.
- **Invocation Trace**: A log entry within a single invocation; key attributes include timestamp, message, and severity level (information, warning, error).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can view invocation history for any function within 5 seconds of clicking its name.
- **SC-002**: Users can drill from function list to invocation summary to individual traces in 3 clicks or fewer.
- **SC-003**: Invocation summary displays at least the 50 most recent invocations per function.
- **SC-004**: Users can identify failed invocations at a glance through clear visual status indicators.
- **SC-005**: Users can navigate back to the function list without re-entering search parameters.

## Assumptions

- The container app has Application Insights connected and collecting function telemetry. This is the standard setup for Azure Functions on Container Apps.
- Invocation data is sourced from Application Insights, which is the same data source used by the `az containerapp function invocations` CLI commands.
- The default time range for invocation history is the last 24 hours, matching the classic Azure Functions portal behavior.
- No real-time streaming is needed for this feature; a point-in-time query on page load is sufficient.
