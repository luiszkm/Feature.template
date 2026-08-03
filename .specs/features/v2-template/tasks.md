# Product.Template v2 — M1 Tasks

**Design:** `.specs/features/v2-template/design.md`  
**Spec:** `.specs/features/v2-template/spec.md`  
**Status:** M1 Complete — M2 Ready  
**Scope:** M1 done (T0–T12). Next: Identity module (M2).

> **Nota:** v2 é template **greenfield**. Referências ao v1 abaixo indicam apenas padrões/comportamento de referência — não há migração de codebase.

---

## M1 Already Complete ✅

| ID | Deliverable | Verified |
|----|-------------|----------|
| T0a | Scaffold v2.1 (`App`, `Shared`, `Features/`) | `dotnet build` |
| T0b | Piloto `RegisterUser.cs` + `User.cs` | 5 App.Tests pass |
| T0c | `ArchitectureTests` (3 tests) | pass |
| T0d | `AGENTS.md`, `features.json`, `nuget.config` | exists |

---

## Execution Plan

### Phase 1: Feature Flags Foundation (Sequential)

```
T1 → T2 → T3 → T4
```

### Phase 2: Host Layer (Parallel after T1)

```
T1 complete, then:
  ├── T5 [P]  TenantMiddleware
  └── T6 [P]  WebApplicationExtensions + ExceptionHandler
       ↓
      T7  Refactor Program.cs
```

### Phase 3: Feature Flag Demo + Tests (Sequential)

```
T7 → T8 → T9
```

### Phase 4: Harness (Parallel)

```
T10 [P]  architecture-vsa.mdc
T11 [P]  new-slice skill
```

### Phase 5: M1 Gate (Sequential)

```
T12  Full verification + STATE update
```

**Full diagram:**

```
T0 (done)
T1 → T2 → T3 → T4 ──→ T8 ──→ T9 ──→ T12
     ├→ T5 [P] ─┐
     └→ T6 [P] ─┴→ T7 ────────────────┘
T10 [P] ─────────────────────────────→ T12
T11 [P] ─────────────────────────────→ T12
```

---

## Task Breakdown

### T1: Add FeatureManagement package

**What:** Add `Microsoft.FeatureManagement.AspNetCore` to App.csproj  
**Where:** `src/App/App.csproj`  
**Depends on:** T0a  
**Reuses:** v1 `src/Api/Api.csproj` (version 4.4.0)  
**Requirement:** V2-FF-04

**Done when:**

- [ ] Package restored from nuget.org
- [ ] `dotnet build` passes

**Tests:** none  
**Gate:** `dotnet build`

---

### T2: Create FeatureFlags constants

**What:** Definir constantes de flags alinhadas à referência v1  
**Where:** `src/App/Host/FeatureFlags.cs`  
**Depends on:** T1  
**Reuses:** v1 `src/Api/Attributes/FeatureGateAttribute.cs` (`FeatureFlags` static class)  
**Requirement:** V2-FF-05

**Done when:**

- [ ] Constants: EnableCaching, EnableAuditTrail, EnableRequestDeduplication, EnableAdvancedLogging, EnableExperimentalFeatures, EnableAI
- [ ] `dotnet build` passes

**Tests:** none  
**Gate:** `dotnet build`

---

### T3: Create FeatureFlagsConfiguration

**What:** Wire `AddFeatureManagement` from config section  
**Where:** `src/App/Host/Configurations/FeatureFlagsConfiguration.cs`  
**Depends on:** T2  
**Reuses:** v1 `FeatureFlagsConfiguration.cs`  
**Requirement:** V2-FF-04

**Done when:**

- [ ] Extension method `AddFeatureFlags(IConfiguration)` registers FeatureManagement
- [ ] Called from `Program.cs` or `Host/Extensions/ServiceCollectionExtensions.cs`

**Tests:** none  
**Gate:** `dotnet build`

---

### T4: Create RequireFeature endpoint extension

**What:** Endpoint filter returning 404 when flag disabled  
**Where:** `src/App/Host/Extensions/EndpointExtensions.cs`  
**Depends on:** T3  
**Reuses:** v1 `FeatureGateActionFilter.cs` behavior  
**Requirement:** V2-FF-01, V2-FF-03

**Done when:**

- [ ] `RequireFeature(this RouteHandlerBuilder, string)` extension exists
- [ ] Returns ProblemDetails 404 when disabled
- [ ] `dotnet build` passes

**Tests:** none (tested in T9)  
**Gate:** `dotnet build`

---

### T5: Extract TenantMiddleware [P]

**What:** Move tenant resolution from Program.cs inline middleware  
**Where:** `src/App/Host/Middleware/TenantMiddleware.cs`  
**Depends on:** T1  
**Reuses:** Current `Program.cs` tenant logic  
**Requirement:** V2-HOST-03

**Done when:**

- [ ] Middleware class with `InvokeAsync`
- [ ] Reads `X-Tenant-Id`; dev fallback tenant unchanged
- [ ] `dotnet build` passes

**Tests:** none (existing RegisterUser tests cover tenant)  
**Gate:** `dotnet build`

---

### T6: Create UseHostPipeline + ExceptionHandler [P]

**What:** Host pipeline extension with flag-gated middleware placeholders + ProblemDetails handler  
**Where:** `src/App/Host/Extensions/WebApplicationExtensions.cs`, `src/App/Host/Extensions/ExceptionHandlerExtensions.cs`  
**Depends on:** T1, T3  
**Reuses:** Current `Program.cs` exception handler; v1 conditional middleware pattern  
**Requirement:** V2-HOST-05, V2-FF-02

**Done when:**

- [ ] `UseHostPipeline(this WebApplication)` registers TenantMiddleware
- [ ] `EnableAdvancedLogging` / `EnableRequestDeduplication` conditions present (stubs OK if middleware not implemented yet)
- [ ] Exception handler extracted from Program.cs
- [ ] `dotnet build` passes

**Tests:** none  
**Gate:** `dotnet build`

---

### T7: Refactor Program.cs to thin orchestrator

**What:** Program.cs ≤40 lines; no inline middleware/exception logic  
**Depends on:** T4, T5, T6  
**Reuses:** T3 AddFeatureFlags, existing AddPlatform/AddInfrastructure/AddEndpoints  
**Requirement:** V2-HOST-01, V2-HOST-04

**Done when:**

- [ ] Program.cs calls `AddFeatureFlags`, `UseHostPipeline`, `MapEndpointsFromAssembly`
- [ ] Program.cs ≤40 lines (excluding usings)
- [ ] `dotnet build` + all existing tests pass

**Tests:** integration (existing suite)  
**Gate:** `dotnet test`

---

### T8: Add FeatureFlags appsettings + stub AI slice

**What:** `FeatureFlags` section in appsettings + minimal `ChatAi.cs` gated slice  
**Where:** `src/App/appsettings.json`, `src/App/Features/Ai/ChatAi.cs`, `features.json`  
**Depends on:** T4, T7  
**Reuses:** v1 `appsettings.json` FeatureFlags section  
**Requirement:** V2-FF-01, V2-FF-06

**Done when:**

- [ ] appsettings has all 6 flags (defaults match v1)
- [ ] `ChatAi.cs` exposes `POST /api/v1/ai/chat` with `.RequireFeature(EnableAI)`
- [ ] `features.json` updated with ChatAi entry + `featureFlag: "EnableAI"`
- [ ] `EnableAI=false` → endpoint not reachable (404)

**Tests:** unit/integration in T9  
**Gate:** manual curl or test T9

---

### T9: Feature flag tests

**What:** Tests proving RequireFeature returns 404 when disabled  
**Where:** `tests/App.Tests/Ai/ChatAiTests.cs`  
**Depends on:** T8  
**Requirement:** V2-FF-01, V2-FF-03

**Done when:**

- [ ] Test: EnableAI=false → 404 (WebApplicationFactory or endpoint filter unit test)
- [ ] Test: EnableAI=true → not 404 (stub may return 501/200)
- [ ] `dotnet test tests/App.Tests` passes (≥7 tests)

**Tests:** integration  
**Gate:** `dotnet test tests/App.Tests`

---

### T10: Create architecture-vsa.mdc rule [P]

**What:** Single Cursor rule documenting v2.1 layout + anti-patterns  
**Where:** `.cursor/rules/architecture-vsa.mdc`  
**Depends on:** None  
**Requirement:** V2-HARNESS-04

**Done when:**

- [ ] Rule covers: flat slices, mirror tests, Host/, no layer folders
- [ ] Rule references `features.json` and `AGENTS.md`

**Tests:** none  
**Gate:** file exists, ≤150 lines

---

### T11: Create new-slice skill [P]

**What:** Skill scaffold creating prod + test pair + features.json entry  
**Where:** `.cursor/skills/new-slice/SKILL.md`  
**Depends on:** None  
**Requirement:** V2-HARNESS-03, V2-HARNESS-05

**Done when:**

- [ ] Skill creates `Features/{Module}/{Slice}.cs` + `tests/App.Tests/{Module}/{Slice}Tests.cs`
- [ ] Skill updates `features.json`
- [ ] Documents optional `.RequireFeature()`

**Tests:** none  
**Gate:** file exists

---

### T12: M1 verification gate

**What:** Full M1 acceptance + update STATE.md traceability  
**Depends on:** T7, T9, T10, T11  
**Requirement:** V2-SCAFFOLD-05..08, V2-FF-01..06, V2-HOST-01..05, V2-HARNESS-01..05

**Done when:**

- [ ] `dotnet build` passes
- [ ] `dotnet test tests/ArchitectureTests` — all pass
- [ ] `dotnet test tests/App.Tests` — all pass
- [ ] Program.cs ≤40 lines verified
- [ ] STATE.md updated: M1 Host + FF = complete
- [ ] spec.md requirement statuses updated for M1 items

**Tests:** full  
**Gate:** `dotnet build && dotnet test`

---

## Task Granularity Check

| Task | Scope | Status |
|------|-------|--------|
| T1 Package ref | 1 csproj change | ✅ Granular |
| T2 Constants | 1 file | ✅ Granular |
| T3 Configuration | 1 file | ✅ Granular |
| T4 RequireFeature | 1 extension file | ✅ Granular |
| T5 TenantMiddleware | 1 middleware | ✅ Granular |
| T6 Host pipeline | 2 extension files | ✅ Cohesive |
| T7 Program refactor | 1 file modify | ✅ Granular |
| T8 appsettings + stub slice | 2-3 files | ✅ Cohesive |
| T9 FF tests | 1 test file | ✅ Granular |
| T10 rule | 1 file | ✅ Granular |
| T11 skill | 1 file | ✅ Granular |
| T12 gate | verification | ✅ Granular |

---

## Diagram-Definition Cross-Check

| Task | Depends On (body) | Diagram Shows | Status |
|------|-------------------|---------------|--------|
| T1 | T0a | T1 after T0 | ✅ |
| T2 | T1 | T1→T2 | ✅ |
| T3 | T2 | T2→T3 | ✅ |
| T4 | T3 | T3→T4 | ✅ |
| T5 | T1 | T1→T5 | ✅ |
| T6 | T1, T3 | T1→T6 | ✅ |
| T7 | T4,T5,T6 | T5,T6→T7, T4→T8 path | ✅ |
| T8 | T4, T7 | T7→T8, T4→T8 | ✅ |
| T9 | T8 | T8→T9 | ✅ |
| T10 | None | parallel | ✅ |
| T11 | None | parallel | ✅ |
| T12 | T7,T9,T10,T11 | all→T12 | ✅ |

---

## Test Co-location Validation

| Task | Code Layer | Required Test | Task Tests Field | Status |
|------|------------|---------------|------------------|--------|
| T1-T6 | Infrastructure/config | none | none | ✅ |
| T7 | Program refactor | regression | integration (existing) | ✅ |
| T8 | New slice stub | integration | deferred to T9 | ✅ (T8+T9 cohesive) |
| T9 | ChatAi slice | integration | integration | ✅ |
| T10-T11 | Docs | none | none | ✅ |
| T12 | Gate | full suite | full | ✅ |

---

## Gate Commands (project)

```bash
dotnet build
dotnet test tests/ArchitectureTests
dotnet test tests/App.Tests
```

---

## Out of M1 (explicit)

| Item | Milestone |
|------|-----------|
| AI full module | **M3 / P3** |
| Login + Identity slices | M1 stretch or M2 start |
| Class Library per module | **Future** (ROADMAP) |
| Serilog, OTel, JWT full | M2 Host Hardening |
