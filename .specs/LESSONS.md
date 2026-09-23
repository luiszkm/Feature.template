# LESSONS - auto-maintained by scripts/lessons.py

> Machine-owned. Do NOT hand-edit. Changes are overwritten on the next `lessons.py` write.
> Canonical state lives in `.specs/lessons.json`. Edit lessons only via the script.
> promote_threshold=2 distinct features · window_days=45 · quarantine_threshold=2

## Confirmed (load these at Plan/Checks)

Corroborated across multiple features. Safe to apply as guidance.

_none_

## Candidates (under observation - do NOT load as guidance yet)

Seen once or not yet corroborated. Tracked, not trusted.

### L-001 - Assert a screen's literal copy and its action at the component level, not only the route that renders it
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: web-frontend
- evidence: src/web/src/app/shared/screens.ts:33 (src/web)
- last seen: 2026-09-13T13:27:24Z

### L-002 - Give each user-visible label, empty state and action its own assertion instead of asserting only that its container exists
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: web-frontend
- evidence: C19 (src/web)
- last seen: 2026-09-13T13:27:24Z

### L-003 - Split a check that names two behaviours into one check per behaviour so a single proof cannot cover half of it
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `.specs` · harmful: 0
- features: web-frontend
- evidence: C15 (.specs)
- last seen: 2026-09-13T13:27:24Z

### L-004 - Give every route the client calls a Surface row, a Coverage row and a check, including logout
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `.specs` · harmful: 0
- features: web-frontend
- evidence: src/web/src/app/shell/shell.ts:102 (.specs)
- last seen: 2026-09-13T13:27:24Z

### L-005 - Give every branch of a decision table its own asserted case, including opt-out context flags
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: web-frontend
- evidence: src/web/src/app/core/http/api.interceptor.ts:29 (src/web)
- last seen: 2026-09-13T13:27:24Z

### L-006 - Re-read the versioned OpenAPI contract before asserting what the API does or does not expose, including query parameters
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `.specs` · harmful: 0
- features: web-frontend
- evidence: src/Api/openapi.json (.specs)
- last seen: 2026-09-13T13:27:24Z

### L-007 - When a decision is superseded, update every place the plan states it - acceptance criteria, flow and impact - not only the door table
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `.specs` · harmful: 0
- features: web-frontend
- evidence: .specs/features/web-frontend/plan.md:53 (.specs)
- last seen: 2026-09-13T13:27:24Z

### L-008 - Check each Swept row against the behaviour its own check proves before writing it
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `.specs` · harmful: 0
- features: web-frontend
- evidence: .specs/features/web-frontend/checks.md:305 (.specs)
- last seen: 2026-09-13T13:27:24Z

### L-009 - Write each proof command with the runner the project's test target actually invokes, and run it once before recording it
- signal: `gate_fail` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: web-frontend
- evidence: .specs/features/web-frontend/checks.md:19 (src/web)
- last seen: 2026-09-13T13:27:24Z

### L-010 - Confirm a test-name filter selects a real test, because the runner exits zero when it matches nothing
- signal: `gate_fail` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: web-frontend
- evidence: .specs/features/web-frontend/checks.md:48 (src/web)
- last seen: 2026-09-13T13:27:24Z

### L-011 - Declare a shared policy's rejection status on every route that carries the policy, not only the routes the feature touches
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `src/Api` · harmful: 0
- features: auth-cookie-contract
- evidence: src/Api/Features/Identity/RegisterUser.cs:69 (src/Api)
- last seen: 2026-09-13T13:45:28Z

### L-012 - Prove a status the contract declares with an assertion that names it, not with a snapshot-equality test that accepts its removal once the snapshot is regenerated
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `tests` · harmful: 0
- features: auth-cookie-contract
- evidence: C19 (tests)
- last seen: 2026-09-13T13:45:28Z

### L-013 - Assert that the calls below a limit succeed as well as the one that trips it, so an off-by-one in the limit cannot pass
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `tests` · harmful: 0
- features: auth-cookie-contract
- evidence: tests/E2ETests/Identity/IdentityAuthE2ETests.cs:184 (tests)
- last seen: 2026-09-13T13:45:28Z

### L-014 - Assert the order of two effects a claim sequences, not only that both happened
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: auth-cookie-contract
- evidence: src/web/src/app/shell/shell.spec.ts:48 (src/web)
- last seen: 2026-09-13T13:45:28Z

### L-015 - Assert an endpoint's success and not-found status codes by calling the mapped route through the test host, not only by invoking the handler directly and checking its return value or thrown exception type.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `api-tests` · harmful: 0
- features: agentes
- evidence: C1,C4,C5,C6,C11,C14,C35,C37,C38 - src/Api/Features/Ai/{CreateAgent,UpdateAgent,DeactivateAgent,GetAgent,CreateAgentFile,DeleteAgentFile}.cs (api-tests)
- last seen: 2026-09-19T19:57:54Z

### L-016 - When a shared test stub fully replaces a service, it also erases the ability to assert the specific arguments the caller passed it; assert dialog copy via a recording spy, not a fixed-answer stub.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `web-tests` · harmful: 0
- features: agentes
- evidence: C22 - src/web/src/testing.ts:180-182, src/web/src/app/features/ai/agents-list.ts:178-180 (web-tests)
- last seen: 2026-09-19T19:57:59Z

### L-017 - When citing a --filter for a data-driven it.each Vitest case, match the test name's actual rendered form including any quoting the interpolation adds, and confirm the run reports at least one executed test before trusting a zero exit code.
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `web-tests` · harmful: 0
- features: agentes
- evidence: C20,C21 - src/web/src/app/shared/list-state.spec.ts:54,93 (web-tests)
- last seen: 2026-09-19T19:58:01Z

### L-018 - Prove per-owner scoping guards such as tenant or agent ownership checks with a negative case that attempts access from a different owner, not only a same-owner happy path.
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `api-tests` · harmful: 0
- features: agentes
- evidence: C39 - src/Api/Features/Ai/AgentFileTools.cs (ReadAgentFileTool), fault F5 (api-tests)
- last seen: 2026-09-19T19:58:06Z

### L-019 - State per-field tie-break ordering rules as a testable claim with at least one case where the primary sort key ties, not only cases proven on distinct primary-key values.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `api-tests` · harmful: 0
- features: agentes
- evidence: C2 - tests/Api.Tests/Ai/CreateAgentTests.cs:263-286 (ListAgentsTests) (api-tests)
- last seen: 2026-09-19T19:58:08Z

### L-020 - Phrase a check's expected cardinality or rate-limit threshold as a reference to its authority (the current size of the set, the configured limiter value), not a hardcoded snapshot number - an unrelated feature that grows the set or reconfigures a shared limiter leaves the literal number stale even though the underlying guard stays correct.
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `checks-authoring` · harmful: 0
- features: auth-cookie-contract
- evidence: checks.md:76 (C19), checks.md:55 (C13) (checks-authoring)
- last seen: 2026-09-19T22:51:16Z

### L-021 - When an e2e test waits for a busy indicator to clear before proceeding, first assert it became busy, not only that it is not busy, so the wait cannot pass without ever having observed the operation start
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: web-frontend
- evidence: C66 (src/web/e2e/users.spec.ts:46) (src/web)
- last seen: 2026-09-19T22:55:15Z

### L-022 - When a table-driven test always mounts a component fresh at its default state, a reset-to-default assertion on that same field is vacuous unless the test first drives the component away from the default before triggering the action
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: web-frontend
- evidence: C67 (src/web/src/app/features/identity/users-list.ts:183) (src/web)
- last seen: 2026-09-19T22:55:17Z

### L-023 - When a UI element's visibility is an OR of a management permission and a self-identity check, assert both the self-without-permission and the neither-self-nor-permission branches, not just one
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: web-frontend
- evidence: src/web/src/app/features/identity/user-detail.ts:80-84 (src/web)
- last seen: 2026-09-19T22:55:19Z

### L-024 - When another feature's change adds an item to a UI component this feature's plan already fixes the arrangement of, recheck that plan's stated membership and order against the shipped component, not only against this feature's own diff
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: web-frontend
- evidence: src/web/src/app/shell/shell.ts:28-62 (src/web)
- last seen: 2026-09-19T22:55:22Z

### L-025 - Give every branch of a decision table its own asserted case, including opt-out context flags
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `front-list-sort` · harmful: 0
- features: web-frontend
- evidence: src/web/src/app/features/identity/users-list.ts:189 / C68 / Faults F3 (front-list-sort)
- last seen: 2026-09-19T22:59:20Z

### L-026 - An architecture guard that maps a route to having a client by URL path alone lets an unrelated HTTP method at the same path mask a missing client for the actual method - compare the full method-and-path pair
- signal: `spec_deviation` · recurrence: 1 feature(s) · scope: `src/web` · harmful: 0
- features: web-frontend
- evidence: src/web/src/app/architecture.spec.ts:34-50 / achado 8-9 / Faults F2 (src/web)
- last seen: 2026-09-19T23:00:25Z

### L-027 - When new code adds a real decision point (a duplicated branch, a new permission check), add it to the checks' Test Policy Evidence list, not only to the checks themselves, so a required proof level is visible for it
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `.specs` · harmful: 0
- features: web-frontend
- evidence: src/web/src/app/features/identity/users-list.ts:183 (checks.md Test policy Evidence list) (.specs)
- last seen: 2026-09-19T23:05:44Z

### L-028 - Inject the clock into any time-window decision and assert the window start at its own layer, because tests that write and read at now cannot tell a daily window from a rolling or unbounded one
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `quota` · harmful: 0
- features: guardrails-agente
- evidence: verification.md round 1 gap 1 - AiRateLimit.cs:80 (quota)
- last seen: 2026-09-22T19:25:09Z

### L-029 - Assert both title and detail of every ProblemDetails a check names, not only the title
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `api-tests` · harmful: 0
- features: guardrails-agente
- evidence: verification.md round 1 gap 3 - AiRateLimitTests.cs:138 (api-tests)
- last seen: 2026-09-22T19:25:09Z

### L-030 - Put the completion log in a finally that wraps every exit of the handler, including the refusals before the main try
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `api-handlers` · harmful: 0
- features: conversas-agente
- evidence: verification.md round 1 - ChatAi.cs:61-68 (C16) (api-handlers)
- last seen: 2026-09-23T11:44:13Z

### L-031 - Prove every claim that names a status code or a query parameter with a test that crosses the HTTP boundary, not only at handler level
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `api-tests` · harmful: 0
- features: conversas-agente
- evidence: verification.md round 1 - C6 C8 C14 C17 C19 C20 C22 level gaps (api-tests)
- last seen: 2026-09-23T11:44:13Z

### L-032 - Never count rows owned by a shared seeded user in HTTP tests, because the host InMemory store is process-wide; create a fresh user for any count assertion
- signal: `gate_fail` · recurrence: 1 feature(s) · scope: `api-tests` · harmful: 0
- features: conversas-agente
- evidence: verification.md round 3 - ChatAiTests.cs:219 (C5) (api-tests)
- last seen: 2026-09-23T11:44:13Z

### L-033 - Disable EF service provider caching in any test container that registers only some modules, because the cached model decides the tenant filters for every InMemory context in the process
- signal: `gate_fail` · recurrence: 1 feature(s) · scope: `api-tests` · harmful: 0
- features: conversas-agente
- evidence: verification.md round 4 - DevBootstrapSeederTests.cs CreateSeedProvider (api-tests)
- last seen: 2026-09-23T11:44:13Z

### L-034 - Give every catch branch that records telemetry its own asserted case, not only the generic exception branch
- signal: `surviving_mutant` · recurrence: 1 feature(s) · scope: `telemetry` · harmful: 0
- features: observabilidade-agente
- evidence: verification.md round 1 - AgentLoop.cs:128-130 (C14 permission_denied) (telemetry)
- last seen: 2026-09-23T13:21:43Z

### L-035 - A test ActivityListener must not sample the source under test when the claim is that the app's own tracer provider creates the spans; run the same test with the switch off as a negative control
- signal: `ac_gap` · recurrence: 1 feature(s) · scope: `telemetry` · harmful: 0
- features: observabilidade-agente
- evidence: verification.md round 1 gap 8 - AiTelemetryE2ETests.cs:19-25 (C17) (telemetry)
- last seen: 2026-09-23T13:21:43Z

### L-036 - Check every span against the attributes the semantic convention marks Required before writing the checks, not only the attributes the plan listed
- signal: `spec_precision_gap` · recurrence: 1 feature(s) · scope: `telemetry` · harmful: 0
- features: observabilidade-agente
- evidence: verification.md round 1 gap 3 - AgentLoop.cs:86-88 (chat span provider) (telemetry)
- last seen: 2026-09-23T13:21:43Z

### L-037 - Toggle host flags that are read during service registration with UseSetting in WebApplicationFactory tests, because in-memory settings only exist after Build
- signal: `gate_fail` · recurrence: 1 feature(s) · scope: `api-tests` · harmful: 0
- features: observabilidade-agente
- evidence: handoff build 2026-09-23 - ObservabilityConfiguration.cs (C18 C19) (api-tests)
- last seen: 2026-09-23T13:21:43Z

## Quarantined (failed when applied - ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
