# Getting started — Product.Template v2

Product.Template v2 é um **template novo** (greenfield). Não há caminho de migração a partir do v1 — use este repositório como ponto de partida para novos produtos.

Arquitetura: **Vertical Slice Architecture (VSA) v2.1** — um projeto `App` + slices planos em `Features/{Module}/`.  
Docs: [architecture/vsa.md](../architecture/vsa.md) · [guidelines.md](../architecture/guidelines.md)

## Instalar como dotnet new template

```bash
# instalar (1x por máquina, a partir do clone deste repo)
dotnet new install .

# gerar novo produto a partir do template
dotnet new product-template -n MeuErp --ProductName "Meu ERP" -o ../MeuErp

# atualizar template após mudanças no repo
dotnet new install . --force

# remover
dotnet new uninstall .
```

`--ProductName` substitui a string `Product.Template` em JWT Issuer/Audience, título OpenAPI, `ServiceName` (observability) e `Directory.Build.props`. Namespace `App` **não muda** (convenção fixa do template). GUIDs de projeto no `.sln` são regenerados automaticamente a cada geração (`.template.config/template.json` → `guids`).

## Pré-requisitos

- .NET 10 SDK
- Docker (PostgreSQL local) ou `Database:UseInMemory=true` para testes

## Quick start

```bash
# 1. Configurar ambiente
copy compose.env.example compose.env
docker compose up -d postgres

# 2. Rodar API (aplica migrations + seed no startup)
cd src/App
dotnet run
```

- API: `http://localhost:5000` (Development)
- OpenAPI/Scalar: `/scalar/v1` (Development)
- Health: `GET /health/live`, `GET /health/ready`

## Credenciais dev (Development)

| Campo | Valor |
|-------|-------|
| Email | `admin@producttemplate.com` |
| Senha | `Admin@123` |
| Tenant header | `X-Tenant: dev` |

Tenants seed: `public`, `dev`.

## Multi-tenancy

- Header: `X-Tenant: dev` ou `X-Tenant-Id: {guid}`
- Subdomínio: configure `MultiTenancy:BaseDomain`
- JWT inclui claim `tenant_id` — validado no pipeline (mismatch → 401)

## Autenticação

- JWT + refresh token rotation
- `security_stamp` no JWT — revogado em delete user / assign-revoke role

## Índice de features

- `features.json` — slices, rotas, feature flags
- `src/App/Features/{Module}/AGENTS.md` — contexto por módulo
- `docs/security/RBAC_MATRIX.md` — políticas e permissões

## Verificação

```bash
dotnet build
dotnet test tests/ArchitectureTests
dotnet test tests/App.Tests
make verify
```

## Onde encontrar código

| Conceito | Path v2 |
|----------|---------|
| Caso de uso (slice) | `src/App/Features/{Module}/{Slice}.cs` |
| Entidade de domínio | `src/App/Features/{Module}/{Entity}.cs` |
| Endpoint HTTP | `IEndpoint` no mesmo slice |
| DI do módulo | `{Module}Module.cs` |
| Seeders | `src/App/Host/Seeders/` |
| Host / segurança | `src/App/Host/` |
| Contexto do módulo | `src/App/Features/{Module}/AGENTS.md` |

## Notas

Itens opcionais / evolução futura:

| Item | Notas |
|------|--------|
| AI produção | `StubLlmService`; Azure OpenAI opcional |
| E2E coverage | Menos testes HTTP que v1 |
| Middleware avançado | IP whitelist, deduplication, audit |
