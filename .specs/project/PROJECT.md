# Product.Template v2

**Vision:** Template .NET 10 com Vertical Slice Architecture otimizada para agentes LLM — menos contexto, mais acertividade, manutenção simples.

**For:** Desenvolvedores e agentes de IA que bootstrapam produtos a partir deste template.

**Solves:** O v1 (Clean Architecture + 16 projetos) exige ~15–18 arquivos e 6–7 hops por mudança de feature, gerando alto custo de tokens e erros de localização para LLMs.

## Goals

- Reduzir custo de contexto por feature em **≥70%** vs v1 (meta: 2–4 arquivos por mudança típica)
- Manter **paridade funcional** com v1 (Identity, Authorization, Tenants, AI, multi-tenancy, JWT/RBAC)
- Seguir **padrão .NET**: produção e testes em projetos separados
- Preservar **feature flags** (`Microsoft.FeatureManagement`) com gate por slice
- Fornecer **harness LLM** (`AGENTS.md` raiz + `Features/{Module}/AGENTS.md`, `features.json`, skills)

## Tech Stack

**Core:**

- Runtime: .NET 10
- Web: ASP.NET Core (Sdk.Web — host + app unificados)
- ORM: EF Core 10
- CQRS: MediatR 14
- Validation: FluentValidation 12
- Feature flags: Microsoft.FeatureManagement.AspNetCore 4.x
- Tests: xUnit + NetArchTest
- Logging/Observability: Serilog, OpenTelemetry (paridade v1 — fases posteriores)

**Key dependencies:** MediatR, FluentValidation, EF Core, FeatureManagement, Scalar/OpenAPI

## Scope

**v2 includes:**

- Arquitetura VSA v2.1 (slice = arquivo plano, Shared = 3 arquivos, Host/ para HTTP)
- Módulos: Identity, Authorization, Tenants (paridade v1)
- Feature flags (runtime gate + middleware condicional)
- Testes espelhados em `App.Tests/` (plano)
- ArchitectureTests (fronteiras namespace/pasta)
- Harness agente (`AGENTS.md` + AGENTS por módulo, `features.json`, `.cursor/` mínimo)
- Docker + CI/CD (paridade v1 — milestone posterior)

**Explicitly out of scope (v2 inicial):**

- Class Library por **feature/slice** (granularidade excessiva)
- Deploy seletivo por módulo via MSBuild (fase evolutiva documentada, não MVP)
- Migração de projetos existentes a partir do v1 (v2 é template novo; v1 é referência)
- Microserviços

## Constraints

- **Technical:** App = 1 projeto Sdk.Web; testes separados; zero testes em `src/App`
- **Compatibility:** v1 (`Product.Template`) é referência funcional opcional — não há path de migração
- **LLM:** `Program.cs` não muda por slice; paths determinísticos documentados em `features.json`

## Reference Documents

- `docs/architecture/vsa.md`
- `docs/architecture/guidelines.md`
- `docs/guides/getting-started.md`
- `docs/security/RBAC_MATRIX.md`
