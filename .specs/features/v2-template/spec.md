# Product.Template v2 — Specification

**Feature ID:** `v2-template`  
**Status:** Approved (2026-08-03)  
**M1 scope:** Scaffold + RegisterUser + **Host + Feature Flags + Harness**  
**P3:** AI module (explicitly deferred from M1/M2)  
**Related:** `.specs/project/PROJECT.md`, `docs/architecture/RELATORIO-VSA-EFICIENCIA-TOKENS.md`

---

## Problem Statement

O Product.Template v1 aplica Clean Architecture com 16 projetos e separação rígida por camada. Isso funciona para times humanos grandes, mas é **caro para agentes LLM**: uma mudança típica (ex.: adicionar campo em RegisterUser) exige ~15–18 arquivos em 6+ áreas, 6–7 hops de navegação e ~5.500–11.000 tokens estimados.

Precisamos de um v2 que preserve qualidade de produção (.NET 10, CQRS, EF, JWT, multi-tenancy, feature flags) com **Vertical Slice Architecture** otimizada para contexto mínimo e paths determinísticos.

---

## Goals

- [ ] Reduzir arquivos por mudança típica de feature para **≤4** (prod + test)
- [ ] Reduzir custo de contexto estimado em **≥70%** vs v1
- [ ] Alcançar **cobertura funcional equivalente** ao v1 nos módulos Identity, Authorization, Tenants (**AI = P3/M3**) — v1 como referência, não origem de migração
- [ ] Manter **feature flags** com gate por slice e middleware condicional
- [ ] Produção e testes em **projetos separados** (padrão .NET)
- [ ] Harness LLM: agente cria slice novo sem perguntar "onde coloco?"

---

## Out of Scope

| Item | Reason |
|------|--------|
| Class Library por slice/feature | Granularidade excessiva; custo LLM |
| Deploy seletivo por módulo (MSBuild) | **Future** — Class Library por bounded context, só se necessário |
| AI module | **P3 / M3** — fora de M1 e M2 |
| Migração de codebases v1→v2 | v2 é template **greenfield**; v1 serve só como referência funcional |
| Remover MediatR/FluentValidation | Stack acordada; manter paridade v1 |
| Co-localizar testes em `src/App` | Viola padrão .NET |
| Microserviços | Fora do escopo do template monolith |

---

## User Stories

### P1: Scaffold VSA v2.1 funcional ⭐ MVP

**User Story**: Como **agente LLM**, quero uma estrutura de pastas determinística com slice em arquivo único, para implementar features lendo ≤4 arquivos.

**Why P1**: Sem scaffold validado, nenhum slice adicional pode seguir o padrão com confiança.

**Acceptance Criteria**:

1. WHEN o repositório v2 é clonado THEN a estrutura SHALL conter `src/App/` (Sdk.Web), `src/App/Shared/`, `src/App/Features/`, `tests/App.Tests/`, `tests/ArchitectureTests/`
2. WHEN um slice é criado THEN produção SHALL ser `src/App/Features/{Module}/{Slice}.cs` (arquivo plano, não pasta)
3. WHEN um slice é criado THEN teste SHALL ser `tests/App.Tests/{Module}/{Slice}Tests.cs` (espelho plano)
4. WHEN entidade é compartilhada por ≥2 slices THEN domínio SHALL ficar em `src/App/Features/{Module}/{Entity}.cs` (sem verbo no nome)
5. WHEN `dotnet build` é executado THEN SHALL completar sem erros
6. WHEN `dotnet test tests/ArchitectureTests` THEN SHALL passar 100%
7. WHEN `dotnet test tests/App.Tests` THEN SHALL passar 100%
8. WHEN `src/App` é inspecionado THEN SHALL conter zero arquivos `*Tests.cs`

**Independent Test**: Piloto RegisterUser compila, testes passam, paths batem com `features.json`.

**Requirements:** `V2-SCAFFOLD-01` … `V2-SCAFFOLD-08`

---

### P1: Slice piloto RegisterUser ⭐ MVP

**User Story**: Como **desenvolvedor**, quero registrar usuário via HTTP POST com validação e persistência, para validar o padrão VSA end-to-end.

**Why P1**: Prova o vertical slice completo antes de implementar o restante do módulo Identity.

**Acceptance Criteria**:

1. WHEN `POST /api/v1/identity/register` com payload válido e tenant resolvido THEN system SHALL retornar `201 Created` com `{ id, email, firstName, lastName }`
2. WHEN email já existe no tenant THEN system SHALL retornar `409 Conflict` (BusinessRuleException)
3. WHEN payload inválido THEN system SHALL retornar `400 Bad Request` (FluentValidation)
4. WHEN tenant não resolvido THEN system SHALL retornar `409` ou `400` com mensagem clara
5. WHEN slice é inspecionado THEN `RegisterUser.cs` SHALL conter Command, Validator, Handler e `IEndpoint` no mesmo arquivo
6. WHEN header `X-Tenant-Id` presente THEN system SHALL usar esse tenant

**Independent Test**: `RegisterUserTests.cs` — validator + handler; curl/HTTP manual opcional.

**Requirements:** `V2-IDN-01` … `V2-IDN-06`

---

### P1: Host layer + Program.cs fino ⭐ MVP

**User Story**: Como **agente LLM**, quero que `Program.cs` não mude ao adicionar slices, para não gastar contexto em infra HTTP.

**Why P1**: Separar concerns HTTP de features reduz hops e erros.

**Acceptance Criteria**:

1. WHEN novo slice é adicionado THEN `Program.cs` SHALL NOT require edits (auto-discovery `IEndpoint`)
2. WHEN infra HTTP é configurada THEN SHALL residir em `src/App/Host/Configurations/`
3. WHEN middleware é registrado THEN SHALL residir em `src/App/Host/Middleware/` ou `Host/Extensions/`
4. WHEN `Program.cs` é lido THEN SHALL ter ≤40 linhas de orquestração (excluindo usings)
5. WHEN exceções de domínio ocorrem THEN Host SHALL mapear para ProblemDetails RFC 9457

**Independent Test**: Adicionar slice dummy; diff de `Program.cs` = zero.

**Requirements:** `V2-HOST-01` … `V2-HOST-05`

---

### P1: Feature Flags ⭐ MVP

**User Story**: Como **operador**, quero desligar módulos/features em runtime via configuração, como no v1.

**Why P1**: Requisito explícito; AI e features experimentais dependem disso.

**Acceptance Criteria**:

1. WHEN `FeatureFlags:EnableAI=false` THEN endpoints AI SHALL retornar `404 Not Found`
2. WHEN flag desligada THEN system SHALL NOT register optional middleware (ex.: deduplication, advanced logging)
3. WHEN slice requer flag THEN SHALL usar `.RequireFeature(FeatureFlags.X)` no endpoint
4. WHEN `appsettings.json` contém seção `FeatureFlags` THEN SHALL ser wired via `AddFeatureManagement`
5. WHEN constantes de flags são usadas THEN SHALL estar em `Host/FeatureFlags.cs` (paridade v1)
6. WHEN `features.json` lista slice THEN SHALL incluir campo `featureFlag` quando aplicável

**Independent Test**: Slice AI com flag off → 404; middleware condicional não registrado.

**Requirements:** `V2-FF-01` … `V2-FF-06`

---

### P1: Harness LLM ⭐ MVP

**User Story**: Como **agente LLM**, quero instruções mínimas e paths fixos, para criar slices sem ambiguidade.

**Why P1**: Diferencial do template; v1 já provou valor do harness.

**Acceptance Criteria**:

1. WHEN agente inicia THEN `AGENTS.md` SHALL existir com ≤120 linhas
2. WHEN agente busca slice THEN `features.json` SHALL mapear `app` + `test` paths
3. WHEN skill `new-slice` é invocada THEN SHALL criar par prod + test nos paths corretos
4. WHEN rule `architecture-vsa.mdc` existe THEN SHALL listar anti-patterns (pastas por camada, testes em src)
5. WHEN novo slice é registrado THEN `features.json` SHALL ser atualizado

**Independent Test**: Agente cria slice `Login` seguindo AGENTS.md sem perguntas.

**Requirements:** `V2-HARNESS-01` … `V2-HARNESS-05`

---

### P2: Identity module completo

**User Story**: Como **desenvolvedor**, quero cobertura Identity equivalente à referência v1, para usar o template em produção sem gaps críticos.

**Why P2**: MVP prova padrão; paridade exige todos os slices Identity.

**Acceptance Criteria**:

1. WHEN comparado com v1 (referência) THEN v2 SHALL expor rotas Identity equivalentes (register, login, refresh, CRUD user)
2. WHEN JWT emitido THEN SHALL seguir claims/policies alinhadas ao v1
3. WHEN cada slice implementado THEN SHALL ter teste espelhado em `App.Tests`
4. WHEN RBAC aplicável THEN endpoint SHALL declarar policy explícita (nunca `[Authorize]` bare)

**Independent Test**: E2E Identity auth flow equivalente ao v1.

**Requirements:** `V2-IDN-07` … `V2-IDN-12`

---

### P2: Authorization + Tenants modules

**User Story**: Como **desenvolvedor**, quero roles, permissions e multi-tenancy como no v1.

**Acceptance Criteria**:

1. WHEN tenant criado THEN provisioning SHALL funcionar equivalente ao v1
2. WHEN roles listadas THEN SHALL respeitar tenant scope
3. WHEN ArchitectureTests rodam THEN módulos SHALL NOT referenciar-se diretamente (MediatR ou Shared only)

**Requirements:** `V2-AUTH-01` … `V2-TEN-04`

---

### P2: Observability + Security (Host)

**User Story**: Como **operador**, quero logs, traces, health checks e JWT como v1.

**Acceptance Criteria**:

1. WHEN app inicia THEN Serilog SHALL estar configurado
2. WHEN `/health` consultado THEN SHALL retornar 200 com checks de DB
3. WHEN OpenTelemetry habilitado THEN traces SHALL incluir requests HTTP

**Requirements:** `V2-OBS-01` … `V2-SEC-03`

---

### P3: AI module + E2E + Docker/CI

**User Story**: Como **desenvolvedor**, quero módulo AI gated por flag, E2E HTTP e pipeline CI como v1.

**Why P3**: AI depende de Host + Feature Flags (M1) e módulos core (M2); explicitamente fora do MVP.

**Acceptance Criteria**:

1. WHEN `EnableAI=true` THEN AI endpoints SHALL funcionar
2. WHEN E2E rodam THEN cobertura mínima Identity + Tenants HTTP
3. WHEN CI roda THEN Trivy gate + architecture tests SHALL passar

**Requirements:** `V2-AI-01` … `V2-CI-03`

---

## Edge Cases

- WHEN slice name conflita com entity name (`User.cs` vs `GetUser.cs`) THEN naming rule SHALL apply: verbo = slice, substantivo = entity
- WHEN slice >150 linhas THEN SHALL split endpoint para `{Slice}Endpoint.cs` (exceção documentada)
- WHEN módulo futuro precisa deploy seletivo THEN documentar evolução para Class Library por bounded context (não implementar no MVP)
- WHEN MediatR license falha em testes THEN testes de handler SHALL usar instância direta do handler
- WHEN feed NuGet privado retorna 401 THEN `nuget.config` SHALL fallback para nuget.org

---

## Requirement Traceability

| Requirement ID | Story | Phase | Status |
|----------------|-------|-------|--------|
| V2-SCAFFOLD-01 | P1 Scaffold | Design | Verified (piloto) |
| V2-SCAFFOLD-02 | P1 Scaffold | Design | Verified |
| V2-SCAFFOLD-03 | P1 Scaffold | Design | Verified |
| V2-SCAFFOLD-04 | P1 Scaffold | Design | Verified |
| V2-SCAFFOLD-05 | P1 Scaffold | Execute | Verified |
| V2-SCAFFOLD-06 | P1 Scaffold | Execute | Verified |
| V2-SCAFFOLD-07 | P1 Scaffold | Execute | Verified |
| V2-SCAFFOLD-08 | P1 Scaffold | Execute | Verified |
| V2-IDN-01 | P1 RegisterUser | Execute | Verified |
| V2-IDN-02 | P1 RegisterUser | Execute | Verified |
| V2-IDN-03 | P1 RegisterUser | Execute | Partial (HTTP manual pending) |
| V2-IDN-04 | P1 RegisterUser | Execute | Verified |
| V2-IDN-05 | P1 RegisterUser | Execute | Verified |
| V2-IDN-06 | P1 RegisterUser | Execute | Verified |
| V2-HOST-01 | P1 Host | Design | Pending |
| V2-HOST-02 | P1 Host | Design | Pending |
| V2-HOST-03 | P1 Host | Design | Pending |
| V2-HOST-04 | P1 Host | Design | Pending |
| V2-HOST-05 | P1 Host | Design | Pending |
| V2-FF-01 | P1 Feature Flags | Design | Pending |
| V2-FF-02 | P1 Feature Flags | Design | Pending |
| V2-FF-03 | P1 Feature Flags | Design | Pending |
| V2-FF-04 | P1 Feature Flags | Design | Pending |
| V2-FF-05 | P1 Feature Flags | Design | Pending |
| V2-FF-06 | P1 Feature Flags | Design | Pending |
| V2-HARNESS-01 | P1 Harness | Execute | Partial (AGENTS.md done) |
| V2-HARNESS-02 | P1 Harness | Execute | Partial (features.json done) |
| V2-HARNESS-03 | P1 Harness | Tasks | Pending |
| V2-HARNESS-04 | P1 Harness | Tasks | Pending |
| V2-HARNESS-05 | P1 Harness | Tasks | Pending |
| V2-IDN-07 … 12 | P2 Identity | — | Pending |
| V2-AUTH-01 … | P2 Authorization | — | Pending |
| V2-TEN-01 … 04 | P2 Tenants | — | Pending |
| V2-OBS-01 … | P2 Observability | — | Pending |
| V2-AI-01 … | P3 AI | — | Pending |
| V2-CI-01 … 03 | P3 CI/CD | — | Pending |

**Coverage:** 38 total, 14 verified/partial, 24 pending

---

## Success Criteria

- [ ] Agente adiciona campo em RegisterUser tocando ≤3 arquivos
- [ ] `make verify` equivalente passa (build + arch tests + app tests + format)
- [ ] Cobertura Identity equivalente à referência v1 demonstrada via E2E
- [ ] Feature flag desliga AI retornando 404
- [ ] Documentação: spec + design + STATE atualizados

---

## Next Steps

1. ~~Review spec → approve~~ ✅ Approved 2026-08-03
2. Design → `.specs/features/v2-template/design.md` ✅
3. Tasks → `.specs/features/v2-template/tasks.md` ✅
4. Execute M1 remaining (Host, FeatureFlags, Harness)
