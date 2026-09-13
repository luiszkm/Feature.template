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

## Quarantined (failed when applied - ignore)

A confirmed lesson that recurred alongside failure. Kept for the maintainer to review.

_none_
