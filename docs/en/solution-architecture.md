# Solution Architecture

## Architecture

The solution follows **Clean Architecture** and **Domain-Driven Design**, built on **ASP.NET Core Minimal APIs**. The core idea: business rules (Domain) never depend on frameworks, databases, or external APIs — dependencies always point inward.

```
GymChatAI.Api            → HTTP endpoints, request/response mapping, auth policies
GymChatAI.Application     → Use cases (handlers), orchestration, ports (interfaces)
GymChatAI.Domain          → Entities, value objects, domain rules, invariants
GymChatAI.Infrastructure  → EF Core, WhatsApp Cloud API, AI providers, background services
```

`Domain` has no project references at all. `Application` depends only on `Domain`. `Infrastructure` and `Api` depend on `Application` and implement its ports (repositories, `IWhatsAppMessageSender`, `IAIAssistantService`, etc.). This means the entire business logic (loyalty rules, onboarding flow, compliance risk calculation) can be unit-tested without a database, a WhatsApp account, or an AI provider.

## Technology Stack

| Layer | Choice | Why |
|---|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs | Lightweight endpoint definitions without controller boilerplate; first-class async support |
| Persistence | SQL Server via EF Core, **or** in-memory | See "Dual Persistence Mode" below |
| Frontend | React + TypeScript (Vite) | Separate SPA, talks to the API over REST + Bearer tokens |
| Messaging | WhatsApp Business Cloud API | Direct HTTP integration, no third-party SDK |
| AI | Gemini, OpenAI, or Azure OpenAI (interchangeable) | See "AI Provider Abstraction" below |
| Auth | ASP.NET Core Identity (opaque Bearer tokens) | Built-in, Data-Protection-encrypted tokens — no hand-rolled JWT code |

## Dual Persistence Mode

The app can run in two modes, decided purely by whether `ConnectionStrings:GymChatDb` is configured:

- **SQL Server mode**: `MigrateAsync()` applies pending EF Core migrations on startup; ASP.NET Core Identity is enabled (a real user store is required for authentication); data survives restarts.
- **In-memory mode**: a process-memory store (`InMemoryDataStore` and a handful of dedicated stores for newer features) backs every repository; authentication is disabled entirely (Identity has nothing to persist users to); data is lost on every restart.

This was a deliberate choice from the very first POC: a developer (or a demo) can clone the repo and run it immediately with zero external dependencies, then graduate to SQL Server without touching a single line of business logic — every repository interface has both an `Ef*Repository` and an `InMemory*Repository` implementation, selected by `ServiceCollectionExtensions.AddGymChatInfrastructure`.

## AI Provider Abstraction

`IAIAssistantService` is a single port with three interchangeable implementations: `GeminiAIAssistantService`, `OpenAIAssistantService`, `AzureOpenAIAssistantService`. The active provider is chosen via configuration (`AiProvider` setting, or auto-detected from whichever provider's API key is populated). None of the business logic (FAQ grounding, conversation history, language detection) knows or cares which provider answered.

This exists for two concrete reasons encountered during development, not just theoretical flexibility:
1. **Provider-side deprecations** — Gemini model names changed mid-project (`gemini-pro` → `gemini-flash-latest`), and having the provider isolated behind one interface meant the fix was contained to one class.
2. **Reliability** — a provider outage or rate limit doesn't require an emergency code change, just a configuration swap.

The AI is *not* given tool-calling or function-calling capability, and is not connected to a formal RAG (retrieval-augmented generation) pipeline with embeddings/vector search. Grounding is simpler: relevant FAQs are found via a basic text-relevance search and injected directly into the prompt alongside the recent conversation history. All flow logic (onboarding, notification preferences, WhatsApp Flows) is decided by our own code before or after the AI call, never by the AI itself.

## WhatsApp Integration Layer

- `WhatsAppCloudApiClient` — sends every outbound message type (plain text, interactive buttons, interactive lists, WhatsApp Flow messages, template messages), talking directly to the Graph API via `HttpClient`.
- `WhatsAppWebhookMapper` — translates Meta's raw webhook JSON into transport-agnostic `IncomingWhatsAppMessage` records, so the Application layer never sees Meta-specific payload shapes.
- `WhatsAppFlowEncryptionService` — implements the WhatsApp Flows Data Exchange encryption protocol (RSA-OAEP to unwrap an AES key, AES-128-GCM for the payload, with the mandatory IV bit-flip on responses), using BouncyCastle rather than .NET's built-in `AesGcm` (which only supports 12-byte nonces — Meta's IV doesn't always fit that constraint).
- A duplicate-message guard inside `WhatsAppCloudApiClient` blocks sending identical text to the same recipient within a short window, protecting the number's WhatsApp quality rating from accidental repeat sends (e.g. a bug causing a retry loop).

## Background Services

Two `IHostedService` implementations run continuously alongside the API:

- `LoyaltyEngineBackgroundService` — runs 15 seconds after startup, then every 24h; evaluates every active gym's automatic campaigns (Welcome, Birthday, Reactivation) and dispatches due messages.
- `PendingAIReplyBackgroundService` — runs every 3 minutes; retries messages that failed to get an AI-generated reply (provider outage, rate limit), up to 5 attempts before giving up and leaving the conversation escalated to a human.

## Soft-Delete Policy

Nothing in the domain is ever hard-deleted, with two narrow exceptions:
1. Draft WhatsApp templates/Flows that were never submitted to (or, for Flows, exist only locally without ever having been created on) Meta's side — there's no external record to preserve.
2. Owned child collections being explicitly replaced by their own aggregate root (e.g. `NotificationPreference.Slots` on `ResetSelections()`, or `WhatsAppFlow.Screens` on `ReplaceScreens()`) — this is deliberate replacement, not accidental cascade loss.

Everything else (`Gym`, `Faq`, `Plan`, `Promotion`, `Campaign`, `ClassType`, `Member`, `Lead`, `Conversation`, `WhatsAppMessageTemplate` once submitted) uses an `IsActive`/status flag with `Activate()`/`Deactivate()` methods, and every foreign key between aggregates uses `DeleteBehavior.NoAction` at the database level to prevent unintended cascades.

## Development & Testing Conveniences

- `dotnet ef migrations has-pending-model-changes` — catches a forgotten migration *before* running the app, instead of failing at startup.
- `POST /api/conversations/reset-for-testing` — closes a test contact's open conversation (and optionally clears their notification preferences), mapped only when `!Environment.IsProduction()`, so retesting the onboarding flow doesn't require manual SQL.
- `GET /health`, `/health/whatsapp`, `/health/ai` — surface credential/token expiry problems directly, instead of forcing a diagnosis session every time a message silently fails to send.

## Observability & Compliance

Three dedicated audit trails feed the Administration Portal's Compliance Dashboard:
- `WhatsAppApiError` — every failed call *we* made to the Graph API (auth, rate limits, malformed requests).
- `WhatsAppDeliveryFailure` — delivery failures *Meta itself* reports after the fact, via the webhook's `statuses` field (a message we successfully sent that still couldn't reach the recipient).
- `PendingAIReply` — every case where the AI failed to answer at all.

The Compliance Dashboard also queries Meta's Graph API directly for the phone number's live `quality_rating` and `whatsapp_business_manager_messaging_limit`, and computes risk flags (e.g. active loyalty campaigns not yet linked to an Approved message template — the only way to legally message someone outside the 24h customer-service window).
