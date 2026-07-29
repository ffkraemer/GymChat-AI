# GymChat AI — Endpoint Documentation

Complete list of every API endpoint, organized by functional area, with what each one solves. This is the foundation for the broader project documentation (architecture, design decisions, etc.) we'll build next.

> Note: every endpoint (except those explicitly marked as public) requires authentication (`Policies.Admin`) and is scoped to the authenticated user's own gym via `GymScopeFilter` — a regular Admin can never reach another gym's data; `PlatformAdmin` deliberately bypasses that restriction.

---

## 1. WhatsApp Webhook (public — called by Meta)

| Method | Route | What it solves |
|---|---|---|
| `POST` | `/webhooks/whatsapp/` | Single entry point for everything Meta sends us: text messages, button/list replies, Flow submissions (`nfm_reply`), and delivery status reports (`statuses`). Processes each message through `ProcessIncomingMessageHandler`, with idempotency (a message with the same `WhatsAppMessageId` is never processed twice, even if Meta redelivers the webhook). |

## 2. Flow Data Exchange (public — called by Meta, protected by encryption)

| Method | Route | What it solves |
|---|---|---|
| `POST` | `/webhooks/whatsapp/flow-data-exchange` | Receives **encrypted** requests (RSA-OAEP + AES-128-GCM) whenever a WhatsApp Flow needs dynamic data (e.g. the gym's class list) or when Meta runs its periodic health check (`ping`). Always returns an encrypted plain-text response (never JSON). Returns `421` when decryption fails - a signal to Meta that the key may be stale. |

## 3. Authentication (ASP.NET Core Identity)

| Method | Route | What it solves |
|---|---|---|
| `POST` | `/api/auth/login` | Login — returns an opaque Bearer token (Data Protection-encrypted), not a JWT. |
| `POST` | `/api/auth/refresh` | Refreshes the token without asking for the password again. |
| `GET` | `/api/auth/me` | Returns the authenticated user's email, name, `gymId`, and roles — used by the frontend to know who's logged in and which gym to manage. |
| `POST` | `/api/auth/register-operator` | `PlatformAdmin` only: creates an Admin account for a specific gym. This is how the platform provisions access for new clients. |

## 4. Gyms

| Method | Route | What it solves |
|---|---|---|
| `GET` | `/api/gyms/{whatsAppPhoneNumberId}` | Resolves which gym owns a given phone number - used internally by message processing. |
| `GET` | `/api/gyms/by-id/{gymId}` | Fetches a gym's data by its id - used by the Portal to show/pre-fill settings (WABA, etc.) without depending on the phone number. |
| `GET` | `/api/gyms/` | `PlatformAdmin` only: lists every gym - needed for the gym picker on the Settings/Templates/Flows pages. |
| `POST` | `/api/gyms/` | `PlatformAdmin` only: creates a new gym (onboarding a new client onto the platform). |
| `POST` | `/api/gyms/{gymId}/whatsapp-business-account` | Sets/updates a gym's WABA (WhatsApp Business Account). Also automatically triggers the webhook subscription (`POST {WABA}/subscribed_apps`), eliminating a manual Graph API Explorer step that used to cause recurring "messages never arrive" issues. |

## 5. FAQs

| Method | Route | What it solves |
|---|---|---|
| `GET` | `/api/faqs/{gymId}` | Lists every FAQ for a gym (including inactive ones, for Portal management). |
| `POST` | `/api/faqs/` | Creates a new FAQ. |
| `PUT` | `/api/faqs/{id}` | Edits an existing FAQ. |
| `POST` | `/api/faqs/{id}/activate` / `/deactivate` | Activates/deactivates a FAQ — we never hard-delete FAQs (a soft-delete policy that runs across the whole codebase). |

*(Internal search/relevance endpoints used by the AI aren't exposed publicly — the AI queries the repository directly.)*

## 6. Class Types

| Method | Route | What it solves |
|---|---|---|
| `GET` | `/api/class-types/{gymId}` | Lists a gym's class types — feed both the older button/list menu and the dynamic options in WhatsApp Flows (`GymClassTypes`). |
| `POST` | `/api/class-types/` | Creates a class type. Special case: if the caller is `PlatformAdmin`, the `gymId` comes explicit in the request (a `PlatformAdmin`'s WABA has no "home" gym of its own); a regular Admin always uses their own `gymId`, ignoring whatever the request body says (protects against an Admin trying to create data for another gym). |
| `PUT` | `/api/class-types/{id}` | Renames it. |
| `POST` | `/api/class-types/{id}/activate` / `/deactivate` | Soft delete, same as FAQs. |

## 7. Members

| Method | Route | What it solves |
|---|---|---|
| `GET` | `/api/members/gym/{gymId}` | Lists a gym's members — used to pick recipients when triggering a manual campaign. |

## 8. Conversations

| Method | Route | What it solves |
|---|---|---|
| `GET` | `/api/conversations/gym/{gymId}` | Lists a gym's conversations (message history). |
| `GET` | `/api/conversations/gym/{gymId}/history` | Full history, including older messages. |
| `POST` | `/api/conversations/reset-for-testing` | **Non-Production only** (`!IsProduction()`). Closes a test number's open conversation and (optionally) clears its notification preferences - removes the need to run manual SQL every time you want to retest the onboarding flow from scratch. |

## 9. Campaigns (loyalty engine)

| Method | Route | What it solves |
|---|---|---|
| `GET` | `/api/campaigns/gym/{gymId}` | Lists a gym's campaigns. |
| `POST` | `/api/campaigns/` | Creates a campaign — a message + a trigger rule (`Welcome`, `Birthday`, `Reactivation`, `Manual`). |
| `POST` | `/api/campaigns/{id}/trigger` | Fires a manual campaign to a chosen list of members — the only way to send a `Manual` campaign (the other three fire on their own, on a schedule). |
| `GET` | `/api/campaigns/gym/{gymId}/history` | Send history (`CampaignMessage`) — idempotency: never sends the same campaign to the same member twice within the same period. |
| `POST` | `/api/campaigns/{campaignId}/link-template` | Links (or unlinks, if `templateId` is `null`) a campaign to an **approved** Meta template. Without this, the campaign sends free text - only allowed within the 24h customer-service window; outside it, this risks rejection or hurting the number's quality rating. This is the actual fix for the permanent warning the Compliance Dashboard used to show. |
| `POST` | `/api/campaigns/{campaignId}/activate` / `/deactivate` | Turns a campaign on/off without deleting it. |

## 10. Compliance Dashboard

| Method | Route | What it solves |
|---|---|---|
| `GET` | `/api/compliance/{gymId}` | Returns the live quality rating (queried directly from Meta), the current messaging limit, and a list of computed **risk flags** (quality rating at risk, frequency-cap errors, error volume, campaigns without a linked approved template). |
| `GET` | `/api/compliance/{gymId}/failures` | The three separate failure categories: failures Meta itself reported (via the delivery status webhook), our own API call failures, and AI reply-generation failures. |

## 11. Message Templates

| Method | Route | What it solves |
|---|---|---|
| `GET` | `/api/templates/{gymId}` | Lists the gym's templates, filtered to the **current** WABA (hides templates from an old WABA, e.g. after switching from a test account to production, without deleting the history). |
| `POST` | `/api/templates/` | Creates a local draft (doesn't exist on Meta's side yet). |
| `POST` | `/api/templates/{id}/submit` | Submits the draft for Meta's review — from this point on, the template can no longer be edited (that's how Meta itself works: the body is immutable once submitted). |
| `DELETE` | `/api/templates/{id}` | Only allows deleting **drafts** — an already-submitted template has to stay, since it's part of Meta's own quality-tracking history. |
| `POST` | `/api/templates/{gymId}/refresh-statuses` | Syncs status (Approved/Rejected/Paused) and the **actual category** Meta assigned - which can differ from what we submitted (e.g. a template meant as "Utility" can get reclassified as "Marketing"). |

## 12. WhatsApp Flows

| Method | Route | What it solves |
|---|---|---|
| `GET` | `/api/flows/{gymId}` | Lists the gym's Flows, also filtered to the current WABA. |
| `POST` | `/api/flows/` | Creates a new Flow on Meta, with a valid placeholder screen (Meta requires at least one properly-structured screen). |
| `POST` | `/api/flows/{id}/publish` | Publishes the Flow (can no longer stay a draft). |
| `DELETE` | `/api/flows/{id}` | Deletes a draft Flow — unlike templates, a Flow exists on Meta's side from the moment it's created, so this calls Meta to delete it there first, before deleting it locally. Published Flows can only be deprecated, never deleted. |
| `POST` | `/api/flows/{gymId}/refresh-statuses` | Syncs the status of every Flow for the gym. |
| `POST` | `/api/flows/{gymId}/encryption-key` | Registers the RSA public key — required before publishing any Flow. Important detail: this is the only Flows endpoint scoped to the **phone number**, not the WABA (unlike every other one — only confirmed after testing against Meta for real). |
| `POST` | `/api/flows/{id}/trigger` | Sends the message that opens the Flow to a test number. |
| `GET` / `POST` | `/api/flows/{id}/screens` | Reads/saves the **structured model** of screens and components (the "Design" mode in the visual editor) — no code, no risk of invalid JSON. |
| `GET` / `PUT` | `/api/flows/{id}/json` | Reads/saves the **raw JSON** of the Flow — an alternative to the structured editor, for anyone who prefers editing/pasting directly (or uploading a `.json` from elsewhere). |
| `POST` | `/api/flows/{id}/endpoint` | Sets this Flow's Data Exchange endpoint URL — required before publishing, whenever the Flow has dynamic data. |

## 13. Credential Health

| Method | Route | What it solves |
|---|---|---|
| `GET` | `/health` | Confirms the app is running and which persistence mode it's in (SQL Server or in-memory). |
| `GET` | `/health/whatsapp` | Confirms whether the WhatsApp access token is still valid — avoids wasting time diagnosing "why isn't the AI replying" when the real cause is an expired token. |
| `GET` | `/health/ai` | Same thing, for the active AI provider's key (Gemini/OpenAI/Azure). |

---

*This document covers endpoints only. The next documents will explain the architectural decisions behind each area (why SQL Server is optional/in-memory, why the loyalty engine uses idempotency, why templates and Flows instead of free text, etc.) — let me know where you'd like to continue.*
