---
name: new-module
version: 1
description: "Scaffold a new VSA bounded context (module) in Product.Template v2. TRIGGER: \"new module\", \"add module\", \"bounded context\". SKIP: use /new-slice when the module folder already exists."
disable-model-invocation: true
---

# Skill: /new-module

Arguments: `{Module}` — e.g. `/new-module Billing`

## Creates
1. `src/App/Features/{Module}/{Entity}.cs` (aggregate + repo + EF config as needed)
2. `src/App/Features/{Module}/{Module}Module.cs` — `Add{Module}Module` (+ policies if RBAC)
3. Register in `Host/Configurations/FeatureModulesConfiguration.cs` via `AddFeatureModules()`
4. Optional: `{Module}Contracts.cs` for cross-slice DTOs
5. First slice via `/new-slice {Module} {Slice}` (or create both in one pass)
6. Tests under `tests/App.Tests/{Module}/`
7. Update `features.json`

## Rules
- Do NOT edit `Program.cs` for endpoint wiring
- Flat files only — no layer folders
- Per-aggregate repos; no public `IRepository<T>`
- Policies named — never bare `[Authorize]`
- Ask before new NuGet packages or migrations

## Verify
```bash
dotnet build
dotnet test tests/ArchitectureTests
dotnet test tests/App.Tests --filter {Module}
```
