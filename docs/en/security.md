# Security

## Authentication

ASP.NET Core Identity, using its built-in **opaque Bearer token** scheme - not a hand-rolled JWT. Tokens are encrypted via ASP.NET Core's Data Protection stack rather than signed/decodable JWTs, and are only meaningful to this specific application instance.

- `POST /api/auth/login` — issues an access token + refresh token.
- `POST /api/auth/refresh` — renews the access token without asking for the password again.
- `GET /api/auth/me` — returns the authenticated identity (email, name, `gymId`, roles), so the frontend never has to decode a token itself.

Authentication is only available in **SQL Server persistence mode** - Identity requires a real, durable user store, which the in-memory mode deliberately doesn't provide (see `solution-architecture.md`). Running in-memory means every endpoint is open, which is acceptable for local development/demos but must never be how a real deployment runs.

## Authorization

Two policies, both role-based:

- **`Policies.Admin`** — any authenticated user with the `Admin` or `PlatformAdmin` role. Used on almost every Portal-facing endpoint.
- **`Policies.PlatformAdmin`** — only `PlatformAdmin`. Reserved for platform-level operations: onboarding a new gym, registering its first operator account, and any action that needs to reach across tenants (setting a WABA id or encryption key on a gym's behalf, without needing that gym's own login).

## Multi-Tenant Isolation

Every gym-scoped endpoint carries a `{gymId}` route parameter, and `GymScopeFilter` (an `IEndpointFilter`) checks it against the caller's own `gym_id` claim before the request reaches the handler. A regular `Admin` whose claim doesn't match the route's `gymId` gets rejected outright — there is no way for one gym's admin to read or write another gym's data by manipulating a URL. `PlatformAdmin` deliberately bypasses this check, since its entire purpose is cross-tenant management.

For the handful of write endpoints that don't have `{gymId}` in the route (e.g. actions keyed by a resource id, like publishing a specific Flow), an equivalent inline ownership check is done in the handler itself, comparing the resource's own `GymId` against the caller's claim.

## Secrets Management

- Local development secrets (WhatsApp access token, AI provider keys, SQL Server connection string) live in `dotnet user-secrets`, never in `appsettings.json` and never committed to source control.
- The RSA private key used to decrypt WhatsApp Flows Data Exchange requests is generated with `openssl` and stored **outside the repository entirely** (not just `.gitignore`'d) — a deliberate structural choice made after an earlier near-miss where an API key almost got committed. If the private key is ever lost without a backup, a new key pair has to be generated and re-registered with Meta; existing Flow sessions simply fail until then (no lasting harm, since Flow sessions are short-lived).
- In a real production deployment, both categories of secret above would live in a managed secrets vault (e.g. Azure Key Vault), not `user-secrets` - see `deployment-guide.md`.

## WhatsApp Flows Encryption

The Data Exchange endpoint (`POST /webhooks/whatsapp/flow-data-exchange`) is itself a security-sensitive surface: it's a public, unauthenticated endpoint (Meta calls it directly), protected entirely by the encryption protocol rather than a Bearer token. Every request is RSA-OAEP + AES-128-GCM encrypted end-to-end; a request that fails to decrypt is rejected with `421` rather than processed, and no partial/best-effort decryption is ever attempted.

## Cross-Origin Access (CORS)

The Administration Portal is a separate frontend origin (a Vite dev server, or a separately-hosted static build in production) calling the API over the network. `AddCors`/`UseCors` is configured explicitly for that origin - without it, the browser blocks every request before it even reaches the API, regardless of how correct the Bearer token is.

## Testing Endpoints Are Never Reachable in Production

`POST /api/conversations/reset-for-testing` (and any similarly convenience-only endpoint added later) is mapped conditionally, guarded by `!app.Environment.IsProduction()`. This is enforced at the routing level, not just by not documenting it - the endpoint genuinely doesn't exist as a route when `ASPNETCORE_ENVIRONMENT=Production`.

## Data Retention as a Security Property

The soft-delete policy described in `solution-architecture.md` doubles as an audit safeguard: nothing a gym's staff does (deactivating a FAQ, an unlinking a campaign's template) destroys the underlying record. Combined with `NoAction` foreign keys everywhere, an accidental or malicious deletion attempt fails loudly rather than cascading silently through related data.
