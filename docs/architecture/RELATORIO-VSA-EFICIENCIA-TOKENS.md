# Relatório — Arquitetura VSA para Máxima Eficiência de Tokens (LLM)

**Data:** 2026-08-03  
**Escopo:** Product.Template v1 → v2  
**Objetivo:** Definir como o template deve ficar para minimizar custo de contexto, maximizar acertividade de agentes e seguir **padrão .NET** (aplicação e testes em projetos separados).

---

## 1. Resumo executivo

A arquitetura v1 (Clean Architecture + CQRS + 16 projetos) é **correta para humanos em times grandes**, mas **caro para LLMs**: uma mudança típica em RegisterUser exige ~18 arquivos em 6 áreas distintas e 6–7 hops de navegação.

A arquitetura v2 adota **Vertical Slice Architecture (VSA)** dentro de `src/App`, com **`Shared/` + `Features/`**, e testes em **projeto separado** que **espelha** a árvore de Features — padrão .NET reconhecido (mirror structure).

| Métrica | v1 (atual) | v2 (alvo) | Redução |
|---------|------------|-----------|---------|
| Projetos em `src/` | 16 | **2** (Api + App) | −87% |
| Projetos de teste | 5 | **3** (App.Tests + Architecture + E2E) | −40% |
| Arquivos de produção por mudança | 10–15 | **1–3** | −80% |
| Arquivos totais (prod + teste) | 12–18 | **2–5** | −70% |
| Profundidade máxima de path | ~8 níveis | **≤6 níveis** | −25% |
| Pastas ambíguas (`Handlers/`, `Mappers/`) | 3× por módulo | **0** | −100% |
| Custo de contexto (classificação) | Alto | **Baixo** | — |

**Princípio rector:** *tudo que muda junho na aplicação, fica junto em `Features/`* — testes seguem o **mesmo caminho relativo** em `tests/App.Tests/`.

---

## 2. Estrutura alvo (padrão .NET + VSA)

### 2.1 Visão geral

```
Product.template/
├── AGENTS.md
├── FEATURES.md                        # índice 1 linha/slice (+ coluna TestPath)
├── docs/architecture/
│   └── RELATORIO-VSA-EFICIENCIA-TOKENS.md
├── .cursor/
│   ├── rules/architecture-vsa.mdc
│   └── skills/new-slice/SKILL.md
│
├── src/                               # ── PRODUÇÃO ──
│   ├── Api/                           # host ASP.NET Core
│   │   ├── Api.csproj                 # → referencia App
│   │   └── Program.cs                 # registra endpoints dos slices
│   │
│   └── App/                           # aplicação (sem testes)
│       ├── App.csproj
│       ├── Shared/                    # cross-cutting global
│       │   ├── Kernel.cs
│       │   ├── ValueObjects.cs
│       │   ├── Exceptions.cs
│       │   ├── Messaging.cs
│       │   ├── Persistence.cs
│       │   ├── Persistence.Identity.cs
│       │   ├── Persistence.Authorization.cs
│       │   ├── Persistence.Tenants.cs
│       │   ├── Security.cs
│       │   ├── MultiTenancy.cs
│       │   └── Audit.cs
│       │
│       └── Features/                  # vertical slices
│           ├── Identity/
│           │   ├── _shared/           # somente se ≥2 slices usam
│           │   │   ├── User.cs
│           │   │   ├── RefreshToken.cs
│           │   │   └── UserRepository.cs
│           │   ├── RegisterUser/
│           │   │   └── RegisterUser.cs
│           │   ├── Login/
│           │   │   └── Login.cs
│           │   └── GetUserById/
│           │       └── GetUserById.cs
│           ├── Authorization/
│           │   ├── _shared/
│           │   │   └── Role.cs
│           │   └── ListRoles/
│           │       └── ListRoles.cs
│           └── Tenants/
│               ├── _shared/
│               │   └── Tenant.cs
│               └── CreateTenant/
│                   └── CreateTenant.cs
│
└── tests/                             # ── TESTES (separados) ──
    ├── App.Tests/                     # espelha src/App/Features/
    │   ├── App.Tests.csproj           # → referencia App
    │   ├── Common/                    # builders, fixtures compartilhados
    │   │   └── UserBuilder.cs
    │   └── Features/
    │       ├── Identity/
    │       │   ├── RegisterUser/
    │       │   │   └── RegisterUserTests.cs
    │       │   ├── Login/
    │       │   │   └── LoginTests.cs
    │       │   └── GetUserById/
    │       │       └── GetUserByIdTests.cs
    │       ├── Authorization/
    │       │   └── ListRoles/
    │       │       └── ListRolesTests.cs
    │       └── Tenants/
    │           └── CreateTenant/
    │               └── CreateTenantTests.cs
    │
    ├── ArchitectureTests/             # NetArchTest — fronteiras
    │   └── ArchitectureTests.csproj
    │
    └── E2ETests/                      # HTTP end-to-end
        └── E2ETests.csproj
```

### 2.2 Projetos e referências (.NET)

```
Api.csproj          → App
App.Tests.csproj    → App
ArchitectureTests   → App, Api
E2ETests            → Api (WebApplicationFactory)
```

| Projeto | Tipo | Conteúdo |
|---------|------|----------|
| `src/Api` | Web | Host, middleware, DI, OpenAPI |
| `src/App` | Class Library | `Shared/` + `Features/` — **zero testes** |
| `tests/App.Tests` | xUnit | Testes unitários + integração por slice |
| `tests/ArchitectureTests` | xUnit + NetArchTest | Regras de namespace/pasta |
| `tests/E2ETests` | xUnit + WAF | HTTP cross-cutting |

### 2.3 Regra de espelhamento (mirror)

Para qualquer slice, o caminho do teste é **determinístico**:

```
src/App/Features/{Module}/{Slice}/{Slice}.cs
         ↓ espelho 1:1
tests/App.Tests/Features/{Module}/{Slice}/{Slice}Tests.cs
```

Documentado em `FEATURES.md`:

```markdown
| Slice        | AppPath                              | TestPath                                      |
|--------------|--------------------------------------|-----------------------------------------------|
| RegisterUser | Features/Identity/RegisterUser/      | tests/App.Tests/Features/Identity/RegisterUser/ |
| Login        | Features/Identity/Login/             | tests/App.Tests/Features/Identity/Login/        |
```

Agente **nunca adivinha** onde fica o teste — substitui `src/App` por `tests/App.Tests` e adiciona `Tests` ao nome do arquivo.

### 2.4 Equivalência ao diagrama de referência

| Diagrama TS | Produção (`src/App`) | Testes (`tests/App.Tests`) |
|-------------|----------------------|----------------------------|
| `order.controller.ts` | Endpoint em `{Slice}.cs` | — |
| `order.service.ts` | Handler MediatR em `{Slice}.cs` | — |
| `order.repository.ts` | `Features/{Module}/_shared/{Entity}Repository.cs` | — |
| `order.dto.ts` | Records no `{Slice}.cs` | — |
| `order.test.ts` | — | `Features/{Module}/{Slice}/{Slice}Tests.cs` |

---

## 3. Regras de ouro (token budget + .NET)

### R1 — 1 arquivo de produção por slice

```
src/App/Features/Identity/RegisterUser/
└── RegisterUser.cs    # Command + Handler + Validator + DTOs + Endpoint
```

**Proibido em `src/App`:**
- Pastas por camada (`Commands/`, `Handlers/`, `Validators/`, `Mappers/`)
- Arquivos de teste (`*Tests.cs`)

### R2 — 1 arquivo de teste por slice (projeto separado)

```
tests/App.Tests/Features/Identity/RegisterUser/
└── RegisterUserTests.cs    # unit + integration do slice
```

Convenção de método: `{Slice}Tests.cs` — sempre suffix `Tests`, nunca `{Slice}.Test.cs`.

### R3 — Profundidade máxima

| Área | Path exemplo | Níveis |
|------|--------------|--------|
| Slice (prod) | `src/App/Features/Identity/RegisterUser/RegisterUser.cs` | 6 |
| Slice (test) | `tests/App.Tests/Features/Identity/RegisterUser/RegisterUserTests.cs` | 6 |
| Shared | `src/App/Shared/Persistence.Identity.cs` | 4 |

### R4 — Um `{Slice}.cs` = unidade de contexto de produção

Ordem fixa dentro do arquivo:

```csharp
// 1. Request/Command record
// 2. Response record
// 3. Validator (FluentValidation)
// 4. Handler (ICommandHandler / IQueryHandler)
// 5. Endpoint extension (MapGroup)
```

Namespace: `Product.Template.App.Features.{Module}.{Slice}`

### R5 — `_shared/` do módulo vs `Shared/` global

| Escopo | Path | Quando |
|--------|------|--------|
| Global (≥2 módulos) | `src/App/Shared/` | Email, DbContext, JWT, Tenancy |
| Módulo (≥2 slices) | `src/App/Features/{Module}/_shared/` | User, Role, Tenant |
| Slice único | inline no `{Slice}.cs` | DTOs exclusivos da operação |

### R6 — EF configs agrupados por módulo em `Shared/`

```
src/App/Shared/Persistence.Identity.cs     # User + RefreshToken
src/App/Shared/Persistence.Tenants.cs
```

Um open carrega todas as configs do módulo.

### R7 — Testes: espelho + tipos por slice

Dentro de `{Slice}Tests.cs`, organizar por região (1 arquivo, previsível):

```csharp
public sealed class RegisterUserTests
{
    // --- Validator tests ---
    // --- Handler tests (integration, InMemory/SQLite) ---
}
```

E2E HTTP permanece em `tests/E2ETests/` — único teste que **não** espelha slice a slice.

### R8 — `FEATURES.md` como índice barato

Colunas obrigatórias: `Slice`, `AppPath`, `TestPath`, `Method`, `Route`, `Policy`.

Lookup antes de navegar — ~50 tokens vs. árvore completa (~500+).

### R9 — `AGENTS.md` slice-first, ≤120 linhas

Regras mínimas:
1. Produção → `src/App/Features/{Module}/{Slice}/`
2. Testes → `tests/App.Tests/Features/{Module}/{Slice}/` (espelho)
3. Shared global → `src/App/Shared/`
4. Gate: `dotnet build && dotnet test tests/App.Tests && dotnet test tests/ArchitectureTests`

### R10 — Sem controllers monolíticos

Cada slice registra endpoint via extension method. `Program.cs` apenas chama `MapRegisterUser()`, `MapLogin()`, etc.

---

## 4. Exemplo concreto: RegisterUser

### 4.1 Produção

```
src/App/Features/Identity/RegisterUser/RegisterUser.cs
src/App/Features/Identity/_shared/User.cs
src/App/Features/Identity/_shared/UserRepository.cs
src/App/Shared/Persistence.Identity.cs
```

### 4.2 Testes (projeto separado, path espelhado)

```
tests/App.Tests/Features/Identity/RegisterUser/RegisterUserTests.cs
tests/App.Tests/Common/UserBuilder.cs
```

### 4.3 Comparação de contexto — adicionar campo `Phone`

| Artefato | v1 | v2 (.NET separado) |
|----------|----|--------------------|
| Arquivos produção | ~12–15 | **3** (RegisterUser.cs, _shared/User.cs, Persistence.Identity.cs) |
| Arquivos teste | 3 projetos | **1** (RegisterUserTests.cs) |
| **Total** | **~15–18** | **4** |
| Hops de navegação | 6–7 | **2** (App + App.Tests, mesmo path relativo) |
| Tokens estimados | ~5500–11000 | **~2000–4000** |
| Redução | — | **~55–65%** |

Perda vs. co-localização total: ~500–800 tokens (1 hop extra para espelho).  
Ganho vs. v1: ainda **dominante**. Compensação: conformidade .NET + `dotnet test` por projeto.

---

## 5. `src/App/Shared/` — conteúdo permitido

| Arquivo | Conteúdo |
|---------|----------|
| `Kernel.cs` | Entity, AggregateRoot, IDomainEvent |
| `ValueObjects.cs` | Email, Password |
| `Exceptions.cs` | NotFoundException, BusinessRuleException |
| `Messaging.cs` | ICommand, IQuery, pipeline behaviors |
| `Persistence.cs` | AppDbContext, HostDbContext, UoW |
| `Persistence.{Module}.cs` | EF configs agrupados |
| `Security.cs` | JWT, RBAC |
| `MultiTenancy.cs` | ITenantContext |
| `Audit.cs` | IAuditableEntity |

**Critério:** usado por ≥2 módulos → `Shared/`. Caso contrário → `_shared/` do módulo ou slice.

---

## 6. Fronteiras (.NET + NetArchTest)

### 6.1 Namespaces

```
Product.Template.App.Shared
Product.Template.App.Features.{Module}._shared
Product.Template.App.Features.{Module}.{Slice}
Product.Template.App.Tests.Features.{Module}.{Slice}   # testes
Product.Template.Api                                    # host
```

### 6.2 Regras ArchitectureTests

| Regra | Enforcement |
|-------|-------------|
| `App` não referencia `Api` | NetArchTest |
| `App.Tests` referencia apenas `App` (+ libs teste) | csproj |
| Features de módulos diferentes não referenciam-se | NetArchTest namespace |
| Comunicação cross-module | MediatR (IQuery/ICommand) ou `Shared/` |
| `Api` referencia `App` | csproj |

### 6.3 Visibilidade

Repos em `_shared/`: `internal` + `InternalsVisibleTo("Product.Template.App.Tests")`.

---

## 7. Harness LLM (adaptado ao layout .NET)

| Artefato | Conteúdo |
|----------|----------|
| `AGENTS.md` | Layout `src/App` + espelho `tests/App.Tests` |
| `FEATURES.md` | AppPath + TestPath por slice |
| `architecture-vsa.mdc` | Anti-patterns + regra de espelho |
| `new-slice/SKILL.md` | Cria **2 paths**: prod + test espelhado |
| `slice-file-template.md` | Template `{Slice}.cs` |
| `slice-test-template.md` | Template `{Slice}Tests.cs` |

Skill `new-slice` **sempre** gera par:

```
src/App/Features/{Module}/{Slice}/{Slice}.cs
tests/App.Tests/Features/{Module}/{Slice}/{Slice}Tests.cs
```

---

## 8. Anti-patterns

| Anti-pattern | Por que custa tokens |
|--------------|---------------------|
| Testes dentro de `src/App/` | Viola .NET; agente confunde prod/test |
| Testes em `tests/UnitTests/Validators/` (v1) | Path não previsível a partir do slice |
| 3 projetos de teste por tipo (Unit/Integration/E2E) | 3× navegação para 1 feature |
| Pasta por camada em App | Agente adivinha caminho |
| 1 arquivo por tipo (Command.cs, Handler.cs…) | N× file opens |
| Controller monolítico | Lê 200 linhas para 1 action |
| EF config em projeto distante (Kernel v1) | Hop extra, erro frequente |
| TestPath que **não espelha** AppPath | Agente não encontra teste |

---

## 9. Granularidade do slice

### ✅ 1 pasta = 1 caso de uso

```
Features/Identity/RegisterUser/
Features/Identity/Login/
Features/Identity/GetUserById/
```

### ❌ 1 pasta = 1 entidade (mini-monólito)

```
Features/Identity/User/   ← 12 handlers misturados
```

---

## 10. Plano de migração

| Fase | Entrega | Validação |
|------|---------|-----------|
| **0** | Este relatório + ADR-001 | Aprovação |
| **1** | Scaffold: Api + App + App.Tests + ArchitectureTests | `dotnet build` |
| **2** | Piloto RegisterUser (prod + test espelhado) | Medir tokens |
| **3** | Harness: AGENTS.md + FEATURES.md + skill `new-slice` | Agente cria par prod/test |
| **4** | Migrar Identity completo | Paridade v1 |
| **5** | Authorization + Tenants | Paridade |
| **6** | E2ETests + descontinuar v1 | `make verify` |

---

## 11. Trade-offs

| Escolha | Trade-off |
|---------|-----------|
| Testes separados (.NET padrão) | +1 hop vs. co-localização; ainda −55–65% vs. v1 |
| Espelho 1:1 App ↔ App.Tests | Path previsível; compensa o hop extra |
| 1 arquivo por slice (prod) | Arquivo ~80–150 linhas; aceitável para LLM |
| 2 projetos src (Api + App) | Padrão .NET ASP.NET Core |

| Mantemos | Como |
|----------|------|
| CQRS + MediatR | No `{Slice}.cs` |
| FluentValidation | No `{Slice}.cs` |
| EF Core | `Shared/Persistence.{Module}.cs` |
| xUnit | `App.Tests` |
| NetArchTest | `ArchitectureTests` |
| E2E HTTP | `E2ETests` separado |

---

## 12. Critérios de aceite

- [ ] `src/App` contém **zero** arquivos `*Tests.cs`
- [ ] Todo slice tem espelho em `tests/App.Tests/Features/...`
- [ ] `FEATURES.md` lista AppPath + TestPath
- [ ] “Adicionar campo Phone” toca **≤4 arquivos** (3 prod + 1 test)
- [ ] **≤4000 tokens** leitura+navegação
- [ ] `dotnet test tests/App.Tests` passa
- [ ] Skill `new-slice` cria par prod/test automaticamente

---

## 13. Decisões fechadas

| # | Decisão | Escolha |
|---|---------|---------|
| 1 | Layout aplicação | `src/App/Shared/` + `src/App/Features/` |
| 2 | Testes | Projeto separado `tests/App.Tests/` espelhando Features |
| 3 | Arquivo prod por slice | 1 (`{Slice}.cs`) |
| 4 | Arquivo test por slice | 1 (`{Slice}Tests.cs`) |
| 5 | E2E | `tests/E2ETests/` separado |
| 6 | Endpoint | Minimal API extension no `{Slice}.cs` |
| 7 | MediatR | Mantido |
| 8 | Namespace | `Product.Template.App.Features.*` |

---

## 14. Conclusão

A arquitetura v2 combina **VSA** (feature folders) com **padrão .NET** (App e Tests em projetos separados), usando **espelhamento 1:1** para manter previsibilidade para LLMs:

```
src/App/Features/{Module}/{Slice}/          → produção
tests/App.Tests/Features/{Module}/{Slice}/  → testes
```

Isso economiza **~55–65% de tokens** vs. v1 (ligeiramente menos que co-localização pura, mas **correto para .NET**), elimina ambiguidade de paths e mantém a janela de contexto focada no slice + seu teste espelhado.

**Próximo passo:** ~~Fase 1–2 — scaffold Api + App + App.Tests + piloto RegisterUser.~~ **Concluído** — ver seção 15.

---

## 15. v2.1 — Otimização máxima (implementado)

### 15.1 Layout final

```
src/App/                              # Sdk.Web (host + aplicação unificados)
├── Program.cs                        # MapEndpointsFromAssembly() — não muda por slice
├── GlobalUsings.cs
├── Shared/
│   ├── Kernel.cs                     # Entity, Email, Exceptions
│   ├── Infrastructure.cs           # DbContext, UoW, DI infra
│   └── Platform.cs                   # MediatR, IEndpoint, Tenant, DI platform
└── Features/
    └── Identity/
        ├── User.cs                   # entidade + repo + EF config (sem verbo)
        └── RegisterUser.cs           # slice completo (com verbo)

tests/App.Tests/                      # espelho plano (sem Features/)
└── Identity/
    └── RegisterUserTests.cs

tests/ArchitectureTests/
features.json                         # índice denso
AGENTS.md                             # ≤60 linhas
nuget.config                          # nuget.org only
```

### 15.2 Regras v2.1 adicionais

| Regra | Descrição |
|-------|-----------|
| Slice = **arquivo plano** | `RegisterUser.cs`, não `RegisterUser/RegisterUser.cs` |
| Sem `_shared/` | Domínio do módulo na raiz: `User.cs` (sem verbo) vs `RegisterUser.cs` (com verbo) |
| Espelho plano | `tests/App.Tests/{Module}/{Slice}Tests.cs` |
| EF config na entidade | `IEntityTypeConfiguration` em `User.cs` |
| Auto-discovery | `IEndpoint` + `MapEndpointsFromAssembly()` — zero edit em `Program.cs` |
| Shared = 3 arquivos | `Kernel.cs`, `Infrastructure.cs`, `Platform.cs` |
| Índice JSON | `features.json` com `app` + `test` paths |

### 15.3 Comparativo de tokens

| Cenário | v1 | v2 | v2.1 (implementado) |
|---------|----|----|---------------------|
| Arquivos (add field, no EF) | 15–18 | 4 | **2** |
| Profundidade path | 8 | 6 | **5** |
| Editar Program.cs | sim | sim | **não** |
| Tokens estimados | 5.5k–11k | 2k–4k | **1.2k–2.5k** |
| Redução vs v1 | — | ~55–65% | **~70–80%** |

### 15.4 Piloto RegisterUser — arquivos

| Papel | Path |
|-------|------|
| Slice | `src/App/Features/Identity/RegisterUser.cs` |
| Domínio | `src/App/Features/Identity/User.cs` |
| Teste | `tests/App.Tests/Identity/RegisterUserTests.cs` |
| Índice | `features.json` |

### 15.5 Verificação

```bash
dotnet build
dotnet test tests/ArchitectureTests   # 3 passed
dotnet test tests/App.Tests           # 5 passed
```

### 15.6 Próximas fases

| Fase | Entrega |
|------|---------|
| **3** | Skill `new-slice` + rule `architecture-vsa.mdc` |
| **4** | Migrar slices Identity restantes (Login, GetUserById…) |
| **5** | Authorization + Tenants |
| **6** | E2ETests + paridade v1 |
