# Getting started — Product.Template v2

Product.Template v2 é um **template novo** (greenfield). Não há caminho de migração a partir do v1 — use este repositório como ponto de partida para novos produtos.

Arquitetura: **Vertical Slice Architecture (VSA) v2.1** — um projeto `Api` + slices planos em `Features/{Module}/`.  
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

`--ProductName` substitui a string `Product.Template` em JWT Issuer/Audience, título OpenAPI, `ServiceName` (observability) e `Directory.Build.props`. Namespace `Api` **não muda** (convenção fixa do template). GUIDs de projeto no `.sln` são regenerados automaticamente a cada geração (`.template.config/template.json` → `guids`).

## Pré-requisitos

- .NET 10 SDK
- Docker (PostgreSQL local) ou `Database:UseInMemory=true` para testes

## Quick start

```bash
# 1. Configurar ambiente
copy compose.env.example compose.env
docker compose up -d postgres

# 2. Rodar API (aplica migrations + seed no startup)
cd src/Api
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

- JWT em memória no cliente + refresh token no cookie `pt_refresh` (`HttpOnly`, `SameSite=Strict`, `Path=/api/v1/identity`)
- Rotação a cada refresh; `POST /api/v1/identity/logout` revoga e apaga o cookie
- O corpo do login **não** devolve o refresh token — nenhum script no browser lhe chega
- `security_stamp` no JWT — revogado em delete user / assign-revoke role

## Front-end

```bash
cd src/web
npm ci
npm start        # :4200, proxy de /api para http://localhost:5080
npm test         # Vitest + MSW
npm run e2e      # Playwright contra a API real
```

Contexto e regras: [src/web/AGENTS.md](../../src/web/AGENTS.md).

## Contrato

`src/Api/openapi.json` é versionado e é a autoridade partilhada entre API, `features.json` e o
front. Depois de mudar um endpoint:

```bash
UPDATE_OPENAPI=1 dotnet test tests/E2ETests    # regenera o documento
dotnet test tests/ArchitectureTests            # falha se features.json divergir
```

## Índice de features

- `features.json` — slices, rotas, feature flags
- `src/Api/Features/{Module}/AGENTS.md` — contexto por módulo
- `docs/security/RBAC_MATRIX.md` — políticas e permissões

## Verificação

```bash
dotnet build
dotnet test tests/ArchitectureTests
dotnet test tests/Api.Tests
make verify
```

## Onde encontrar código

| Conceito | Path v2 |
|----------|---------|
| Caso de uso (slice) | `src/Api/Features/{Module}/{Slice}.cs` |
| Entidade de domínio | `src/Api/Features/{Module}/{Entity}.cs` |
| Endpoint HTTP | `IEndpoint` no mesmo slice |
| DI do módulo | `{Module}Module.cs` |
| Seeders | `src/Api/Host/Seeders/` |
| Host / segurança | `src/Api/Host/` |
| Contexto do módulo | `src/Api/Features/{Module}/AGENTS.md` |

## Notas

Itens opcionais / evolução futura:

| Item | Notas |
|------|--------|
| AI produção | `Ai:Llm:Provider` = `OpenRouter` (default) ou `MicrosoftAgentFramework`; chave `Ai:Llm:ApiKey` / `AI_LLM_API_KEY` (placeholder em `compose.env.example`; valor em `compose.env` ou user-secrets). Testing e Development sem chave usam `StubLlmService`. Production + `EnableAI=true` sem chave falha no arranque. |
| AI guardrails | Cada saída de tool chega ao modelo entre `<tool_output>` e `</tool_output>`, truncada a `Ai:Guardrails:MaxToolOutputChars` (default `16000`). O filtro de conteúdo é `IContentGuard`; o default `AllowAllContentGuard` não bloqueia nada — registe outra implementação (ex.: Azure AI Content Safety) para bloquear mensagem, saída de tool ou resposta. |
| AI limites | `POST /ai/chat` e `POST /ai/comparisons` partilham um balde por tenant: `Ai:RateLimit:PermitLimit` (default `30`) por `Ai:RateLimit:WindowSeconds` (`60`) → `429`. Quota diária opcional `Ai:Quota:DailyTokensPerTenant` (default `0` = desligada), tokens in+out desde 00:00 UTC. **Os defaults são escolhas do template: reveja-os antes de servir utilizadores reais.** |
| AI conversas | O transcript do chat é guardado no servidor (`AiConversations`, `AiConversationItems`) e cada utilizador só vê o seu. Retenção `Ai:Conversations:RetentionDays` (default `90`, `0` desliga), purge a cada `PurgeIntervalHours` (`24`); `HistoryWindow` (`20`) e `MaxItems` (`200`) limitam o que é reenviado e o tamanho de uma conversa. **Os 90 dias são uma escolha do template, não uma decisão legal: reveja-os antes de servir utilizadores reais.** |
| E2E coverage | Menos testes HTTP que v1 |
| Middleware avançado | IP whitelist, deduplication, audit |
