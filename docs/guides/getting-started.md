# Getting started — Product.Template v2

Product.Template v2 é um **template novo** (greenfield). Não há caminho de migração a partir do v1 — use este repositório como ponto de partida para novos produtos.

Arquitetura: **Vertical Slice Architecture (VSA) v2.1** — um projeto `App` + slices planos em `Features/{Module}/`.

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
- OAuth Microsoft (opcional): `MicrosoftAuth:Enabled=true` + secrets via user-secrets/Key Vault

## Índice de features

- `features.json` — slices, rotas, feature flags
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

## Comparação com v1 (referência opcional)

O v1 (`Product.Template` — Clean Architecture, ~16 projetos) serve apenas como **referência de funcionalidades**, não como origem de migração.

| Aspecto | v1 | v2 |
|---------|----|----|
| Projetos | Domain/Application/Infrastructure por módulo | 1 App + testes |
| Organização | Por camada | 1 arquivo por caso de uso |
| DB | SQL Server / PostgreSQL | PostgreSQL |
| Índice | Controllers + docs | `features.json` |

Itens ainda não portados do v1 (não bloqueiam uso greenfield):

| Item | Notas |
|------|--------|
| AI produção | `StubLlmService`; Azure OpenAI opcional |
| E2E coverage | Menos testes HTTP que v1 |
| Middleware avançado | IP whitelist, deduplication, audit |
