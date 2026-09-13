# Refresh em cookie httpOnly e guarda de contrato

Sources:

- conversa - endurecer a auth com cookie `HttpOnly` e tornar o documento OpenAPI autónomo
- `.specs/features/web-frontend/plan.md` - door 3 (sessão no browser) e door 5 (cliente à mão), ambas reabertas aqui
- `src/Api/Features/Identity/Login.cs` · `RefreshAccessToken.cs` - a rotação que já existe e que este trabalho mantém

## Problem

O refresh token chega ao browser no corpo da resposta e fica em `localStorage`, onde qualquer XSS
o lê. A rotação e o `security_stamp` limitam a janela, não a fecham: um token roubado vale até ao
próximo refresh, e nada impede o atacante de o usar primeiro. O front não tem forma de o evitar —
`LoginHandler` devolve-o no corpo e não emite `Set-Cookie`, logo a decisão está no servidor.

Em paralelo, o documento OpenAPI só é mapeado em Development (`OpenApiHostConfiguration`), o que
deixa o contrato sem autoridade fora da máquina do programador: nada em CI compara as rotas que a
API expõe com as que o índice e o front assumem. Uma rota renomeada só aparece quando um ecrã
falha em runtime.

Quando isto estiver entregue, o browser nunca vê o refresh token, terminar sessão revoga-o de
facto, e uma divergência entre `features.json`, a API e os clientes do front falha no build.

## Out of scope

| Excluded | Why |
| --- | --- |
| BFF com YARP | fecha o mesmo buraco a um custo de projeto e deploy que não foi pedido |
| Tipos TypeScript gerados do OpenAPI | escolhido manter os clientes à mão; a guarda de contrato passa a cobrir o risco |
| Rotação do access token para cookie | o bearer em memória não sobrevive a um reload e não é o vetor em causa |
| Revogação em massa por utilizador | `security_stamp` já cobre o caso que motivou esta feature |

## Assumptions

| Assumption | Chosen default | Rationale | Confirmed? |
| --- | --- | --- | --- |
| Nome do cookie | `pt_refresh` | prefixo do produto, sem `__Host-` porque o `Path` é estreito | n |
| Âmbito do cookie | `Path=/api/v1/identity` | só as três rotas que o usam o recebem; reduz exposição face a `Path=/` | n |
| `SameSite` | `Strict` | front e API são sempre same-origin (proxy em dev, nginx em produção) | n |
| `Secure` | ligado quando o pedido chega por HTTPS | `http://localhost` em dev descartaria um cookie `Secure` | n |
| Logout | rota nova `POST /api/v1/identity/logout`, anónima | um utilizador com access token expirado ainda tem de conseguir revogar o refresh | n |
| `openapi.json` | **versionado**, regenerado por um teste que corre o host em memória | geração no build exigiria desligar o fail-fast de configuração da API (ver door 6); versionado, a mudança de contrato aparece no diff do PR | n |
| Corpo do refresh | continua aceite mas ignorado | mantém `RefreshTokenCommand` e os seus testes de validação intactos | n |

**Open questions:** none - all resolved or logged above.

## Criteria

### S1: Refresh token fora do alcance do browser (P1)

**Acceptance Criteria**

1. WHEN o login sucede THEN o sistema SHALL emitir `Set-Cookie` com `pt_refresh`, `HttpOnly`, `SameSite=Strict` e `Path=/api/v1/identity`, e o corpo da resposta SHALL não conter `refreshToken`
2. WHEN o refresh é chamado THEN o sistema SHALL ler o token do cookie `pt_refresh`, rodá-lo e reemitir o cookie com o token novo
3. IF o pedido de refresh não traz o cookie `pt_refresh` THEN o sistema SHALL devolver `401`
4. WHEN o logout é chamado com o cookie presente THEN o sistema SHALL revogar esse refresh token, apagar o cookie e devolver `204`
5. IF o logout é chamado sem cookie THEN o sistema SHALL devolver `204` e não alterar nenhum registo
6. WHILE o pedido chega por HTTPS o sistema SHALL marcar o cookie com `Secure`
7. The system SHALL enviar todas as requisições do front para `/api/v1/**` com `withCredentials` a `true`
8. The system SHALL não escrever nenhum refresh token em `localStorage` nem em `sessionStorage`

**Independent test:** login pela UI, confirmar que `localStorage` não contém o token e que o pedido de refresh sucede depois de recarregar a página.

### S2: Contrato com autoridade fora de Development (P1)

**Acceptance Criteria**

9. WHEN `dotnet test tests/E2ETests` corre THEN o sistema SHALL comparar o documento servido pelo host de testes com `src/Api/openapi.json` e falhar se diferirem
10. IF uma rota de `features.json` não existir em `openapi.json` com o mesmo método THEN o sistema SHALL falhar `dotnet test tests/ArchitectureTests`
11. IF `openapi.json` expuser uma rota `/api/v1/**` ausente de `features.json` THEN o sistema SHALL falhar `dotnet test tests/ArchitectureTests`
12. IF um cliente do front chamar um caminho ausente de `openapi.json` THEN o sistema SHALL falhar `npm test`

**Independent test:** renomear uma rota num endpoint e confirmar que o build de testes falha antes de qualquer ecrã ser aberto.

### S3: Solução compilável na raiz (P2)

**Acceptance Criteria**

13. WHEN `dotnet build` corre na raiz do repositório THEN o sistema SHALL compilar os quatro projetos sem `MSB1003`

**Independent test:** `make verify` numa árvore limpa.

## Traceability

| ID | Slice | Criteria | Status |
| --- | --- | --- | --- |
| AUTH-01 | S1 | 1, 2, 3, 4, 5, 6, 7, 8 | Pending |
| CONTRACT-01 | S2 | 9, 10, 11, 12 | Pending |
| BUILD-01 | S3 | 13 | Pending |

## Observable

| Surface | Decision | Landing |
| --- | --- | --- |
| API `POST /api/v1/identity/login` | forma da resposta | AC 1 - corpo perde `refreshToken`, ganha `Set-Cookie` |
| API `POST /api/v1/identity/login` | forma do erro e códigos | existing - ProblemDetails já emitido por `ExceptionHandlerExtensions` |
| API `POST /api/v1/identity/refresh` | quem pode chamar | AC 2, AC 3 - quem apresentar o cookie |
| API `POST /api/v1/identity/refresh` | forma do erro e códigos | AC 3 |
| API `POST /api/v1/identity/logout` | forma da resposta | AC 4, AC 5 - `204` sem corpo nos dois casos |
| API `POST /api/v1/identity/logout` | quem pode chamar | AC 4 - anónima; revoga apenas o token apresentado |
| API `POST /api/v1/identity/logout` | comportamento no rate limit | existing - política `auth`, igual a login e refresh |
| API `/api/v1/identity/**` | versionamento | n/a - `v1` fixo no caminho, sem negociação |
| screen `login` | estado de erro | existing - AC 2 e AC 3 da feature web-frontend continuam a valer |
| screen `shell` | ação destrutiva confirma | existing - Sair já pede confirmação; passa a chamar `logout` antes de limpar |
| command `dotnet build` | formato de saída e códigos de saída | AC 9, AC 13 |
| command `dotnet test` | formato de saída e códigos de saída | AC 10, AC 11 |
| document `docs/security/RBAC_MATRIX.md` | estrutura, e o que o leitor faz a seguir | AC 4 - a rota nova entra na matriz no mesmo PR |

## Flow

Reutiliza a rotação e o hash que `RefreshTokenHandler` e `RefreshToken.HashToken` já fazem: muda o
transporte do token, não o seu ciclo de vida. A revogação do logout usa o `TryRevokeAsync` que já
existe, em vez de um caminho novo.

1. browser `POST /api/v1/identity/login` -> `Api.Features.Identity` (exists) - autentica, cria `RefreshToken` (exists)
2. `LoginEndpoint` (exists) - escreve `Set-Cookie` (door 1) e devolve o corpo sem o token
3. browser `POST /api/v1/identity/refresh` com o cookie -> `RefreshTokenEndpoint` (exists) - lê o cookie, delega no handler (exists), reescreve o cookie
4. browser `POST /api/v1/identity/logout` -> `Logout` (new, door 2) - revoga via `IRefreshTokenRepository` (exists), apaga o cookie
5. out: `204`; `web/core/session` (exists) limpa o estado local e navega para `/login`
6. testes: `OpenApiDocumentTests` (new, door 6) pede `/openapi/v1.json` ao host em memória e compara com `src/Api/openapi.json` versionado, lido por `ArchitectureTests` (exists) e por `architecture.spec.ts` (exists)

## Relations

`None - no stored-data shape change` — `RefreshToken` mantém colunas, índices e ciclo de vida. O
que muda é por onde o valor viaja.

## Surface

| Route | In | Out | Status |
| --- | --- | --- | --- |
| `POST /api/v1/identity/login` | `email`, `password` | `accessToken` · `expiresIn` · `user` · `Set-Cookie: pt_refresh` | `200`, `400`, `401`, `409`, `429` |
| `POST /api/v1/identity/refresh` | cookie `pt_refresh` | `accessToken` · `expiresIn` · `user` · `Set-Cookie: pt_refresh` | `200`, `401`, `404`, `409`, `429` |
| `POST /api/v1/identity/logout` | cookie `pt_refresh` | sem corpo · `Set-Cookie` expirado | `204`, `429` |

## Landing

| One-way door | Literal shape | Alternative rejected |
| --- | --- | --- |
| refresh token passa a cookie | `Set-Cookie: pt_refresh=<raw>; HttpOnly; SameSite=Strict; Path=/api/v1/identity; Max-Age=<dias*86400>; Secure quando HTTPS` | manter no corpo com guarda em `localStorage` - legível por qualquer XSS, que é o problema que motiva esta feature; supersede a door 3 de `web-frontend` |
| corpo do login deixa de expor o token | `AuthTokenResponse(accessToken, tokenType, expiresIn, user)` - sem `refreshToken` | manter o campo a `null` para compatibilidade - um campo que existe e nunca vale nada convida o próximo cliente a lê-lo |
| rota nova `POST /api/v1/identity/logout` | anónima, rate-limited pela política `auth`, `204` com ou sem cookie | deixar o cookie expirar sozinho - o refresh continuaria válido depois de o utilizador sair |
| documento OpenAPI gerado no build | `Microsoft.Extensions.ApiDescription.Server` com `OpenApiDocumentsDirectory=$(MSBuildProjectDirectory)`, artefacto não versionado | gerar levantando a app em CI - obriga o pipeline a arrancar em Development só para ler o contrato |
| **supersede a linha acima:** documento versionado, regenerado pelo host de testes | `src/Api/openapi.json` no repositório; `OpenApiDocumentTests` regenera com `UPDATE_OPENAPI=1` e falha quando diverge; `MapOpenApi()` passa a correr em Development **e** Testing, Scalar continua só em Development | geração no build - o `dotnet-getdocument` executa `Program.Main`, que falha no fail-fast de `AddInfrastructure` ("A database connection string is required"); a alternativa era enfraquecer esse guard, que tem testes próprios em `FailFastConfigurationTests` |
| ficheiro de solução na raiz | `Product.Template.sln` com os quatro projetos e os GUIDs já listados em `.template.config/template.json` | manter sem solução - `dotnet build`, `make verify` e o passo Build do CI falham com `MSB1003` |

## Impact

| Front | What changes |
| --- | --- |
| domain | novo termo: `pt_refresh` - o cookie que passa a transportar o refresh token; quem ramifica nele é `RefreshTokenEndpoint` e o `Logout` |
| domain | termo existente: `AuthTokenOutput` mantém `RefreshToken` como saída interna do handler, mas deixa de ser o que sai na rede - `LoginEndpoint`, `RefreshTokenEndpoint` e os testes E2E .NET são quem lê isto hoje |
| stored data | nada a migrar: `RefreshToken` fica igual, e os tokens já emitidos continuam válidos até expirarem |
| contrato | `features.json` ganha a linha `Logout`; `docs/security/RBAC_MATRIX.md` ganha a rota |
| front | `SessionStore` deixa de guardar `refreshToken`; `pt.auth` passa a conter apenas `tenantKey` e `user`; o interceptor passa a enviar `withCredentials` |
| testes | `tests/Api.Tests/Identity/LoginTests.cs` e `RefreshAccessTokenTests.cs` continuam a valer ao nível do handler; `tests/E2ETests/Identity/IdentityAuthE2ETests.cs` passa a desserializar `AuthTokenResponse` |
| build | `src/Api/openapi.json` passa a ser um ficheiro versionado, regenerado com `UPDATE_OPENAPI=1 dotnet test tests/E2ETests` |
| testes | `tests/Api.Tests/Common/TestServiceFactory.cs` regista `LogoutHandler` em `CreateWithAuth` |
