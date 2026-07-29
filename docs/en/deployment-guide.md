# Deployment Guide

## Local Development Setup

The full daily startup sequence:

1. Start Docker Desktop, then bring up SQL Server: `docker compose up -d` (repo root) — wait for `gymchatai-sqlserver` to report `healthy`.
2. Check for a forgotten migration before running: `dotnet ef migrations has-pending-model-changes --startup-project ..\GymChatAI.Api` (run from `src/GymChatAI.Infrastructure`).
3. Run the API: `dotnet run --project src\GymChatAI.Api`.
4. In a separate terminal, expose it publicly for Meta's webhook: `ngrok http 5277`.
5. If the ngrok URL changed since last time, update the Callback URL in Meta's app dashboard (**Use cases → Customize → Basic setup → Step 2 → Configure Webhooks**).
6. Confirm health: `GET /health`, `/health/whatsapp`, `/health/ai` should all report OK before testing anything by hand.
7. (Optional) Start the Administration Portal: `cd frontend && npm run dev`.

## Persistence Mode

Controlled entirely by whether `ConnectionStrings:GymChatDb` is set:

- **Present** → SQL Server mode. Migrations apply automatically on startup (`MigrateAsync()`); ASP.NET Core Identity (and therefore authentication) is enabled.
- **Absent** → in-memory mode. No external dependency needed at all, but data doesn't survive a restart and authentication is disabled - this mode exists purely for zero-friction local exploration and quick demos, never for anything resembling production.

## One-Time WhatsApp Flows Setup (per gym)

1. Generate an RSA key pair with `openssl` (2048-bit, password-protected), stored outside the repository.
2. Put the private key + password into `user-secrets` (`WhatsAppFlow:PrivateKeyPem`, `WhatsAppFlow:PrivateKeyPassword`).
3. Register the public key from the Administration Portal (Settings page) - this calls Meta's `whatsapp_business_encryption` endpoint automatically.
4. Set the gym's WABA id, also from the Portal - this automatically subscribes the app to receive that WABA's webhook events, removing what used to be a manual Graph API Explorer step.

## Configuration Reference

| Setting | Where it comes from | Notes |
|---|---|---|
| `ConnectionStrings:GymChatDb` | user-secrets (dev) / managed config (prod) | Presence alone decides the persistence mode |
| `WhatsApp:AccessToken` | user-secrets / managed config | Use a System User permanent token in any long-lived environment - a temporary token expires in ~24h |
| `WhatsAppFlow:PrivateKeyPem` / `PrivateKeyPassword` | user-secrets / managed secrets vault | Never in `appsettings.json` |
| `AiProvider` | `appsettings.json` (not secret) | Explicit override; otherwise auto-detected from whichever provider's API key is populated |
| `Gemini:ApiKey` / `OpenAI:ApiKey` / `AzureOpenAI:*` | user-secrets / managed config | Only the active provider's key needs to be set |

## Health Checks Before Testing Anything

Always check `GET /health`, `/health/whatsapp`, `/health/ai` before assuming a bug - a huge fraction of "why isn't this working" sessions during development turned out to be an expired WhatsApp token or an exhausted AI provider quota, not application logic.

## What's Different in a Real Production Deployment

Everything above describes local development. A production deployment differs in a few concrete ways:

- No Docker Desktop / ngrok - the database is a managed SQL Server instance, and the API is reachable on a stable, real public URL (no tunnel, no URL that changes on every restart).
- Secrets (WhatsApp token, AI provider keys, the Flows RSA private key) live in a managed secrets vault (e.g. Azure Key Vault) rather than `user-secrets`.
- `ASPNETCORE_ENVIRONMENT=Production` - this alone disables the testing-convenience endpoints (see `security.md`) at the routing level.
- The WhatsApp access token should always be a System User **permanent** token (generated once via Meta Business Manager, "Never expire"), not the ~24h temporary token used for quick local testing.
- The Administration Portal frontend is built (`npm run build`) and served as a static bundle from real hosting, rather than Vite's dev server - and the API's CORS configuration needs to allow that real origin, not just `localhost`.
