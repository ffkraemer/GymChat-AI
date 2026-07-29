# WhatsApp Integration

## Platform

WhatsApp Business Cloud API — a direct HTTP integration against Meta's Graph API, no third-party SDK or BSP (Business Solution Provider) layer in between.

## Inbound: The Webhook

`POST /webhooks/whatsapp/` is the single entry point for everything Meta sends us. `WhatsAppWebhookMapper` translates Meta's raw JSON into transport-agnostic records before anything else touches it, so the rest of the system never depends on Meta's specific payload shapes. Four distinct things arrive through the same webhook:

1. **Plain text messages** — the common case, routed to the AI assistant (grounded by FAQs) unless a menu/Flow/onboarding step intercepts it first.
2. **Interactive replies** — a tap on a button or list row. Carries no free text at all, only the id of whichever option was chosen; `Message.Content` can never be empty (a domain invariant), so a readable placeholder (`[Menu: option_id]`) is used for these in the conversation history.
3. **Flow submissions** (`interactive.type == "nfm_reply"`) — the final answer set from a completed WhatsApp Flow, delivered as a JSON string inside the webhook payload (a separate mechanism from the encrypted Data Exchange endpoint - see below).
4. **Delivery status reports** (`statuses`) — Meta confirming, after the fact, whether a message we sent was actually delivered. This was a real gap for a while: a message we successfully sent could still silently fail to reach the recipient (wrong number, blocked business), and we had no visibility into that until this was wired up - it now feeds `WhatsAppDeliveryFailure`, one of the Compliance Dashboard's three failure categories.

Every inbound message is checked against `WhatsAppMessageId` before processing, so a webhook redelivery (Meta does retry) never processes the same message twice.

## Outbound: Message Types

`WhatsAppCloudApiClient` sends five distinct message shapes, all through the same underlying `messages` endpoint with a different `type` field: plain text, interactive buttons (up to 3 options), interactive lists (up to 10 rows), WhatsApp Flow messages (opens a native form), and template messages (see below). A duplicate-message guard blocks sending identical text to the same recipient within a short window, protecting the number's quality rating from accidental repeat sends.

## The 24-Hour Window and Why It Drives Everything

WhatsApp only allows free-form text when there's an open **customer-service window** - i.e. the recipient messaged the business within the last 24 hours. Outside that window, a business-initiated message *must* use a pre-approved message template, or Meta will reject it (or, worse, silently let it through while damaging the number's quality rating over time). This single rule is why two entire features exist:

- **Message Templates** (below) — so loyalty campaigns and other business-initiated messages can be sent compliantly outside that window.
- **The Compliance Dashboard** — flagging, by name, any active campaign that's still sending free text instead of an approved template.

## Message Templates

Managed entirely from the Administration Portal instead of Meta Business Manager: create a draft (using the same `{VariableName}` placeholder syntax as loyalty campaigns), submit it for Meta's review, and track its status (Draft → PendingApproval → Approved/Rejected/Paused/Disabled). Two details that only surfaced from testing against Meta directly:

- Meta can **silently reclassify** a template's category after review (a template submitted as "Utility" can come back as "Marketing") - `ActualCategory` is synced separately from what we submitted, so this is visible rather than a silent surprise.
- Once submitted, a template's body is immutable - editing means creating a new template, not modifying the existing one. Deletion is therefore only ever allowed for drafts that never made it to Meta.

## WhatsApp Flows

Meta's native multi-screen form experience - the richer alternative to a chain of button/list menus, supporting real multi-select (`CheckboxGroup`), dropdowns, and free-text inputs in a single native screen.

### Why Flows exist alongside the older button/list menu

The original onboarding/preferences menu (still in place, `OnboardingFlowHandler`) is a hand-written state machine driven by chained buttons and lists - functional, but limited to single-choice steps and no real multi-select (working around that required a "want to add another?" loop). Flows solve that properly, at the cost of two real prerequisites: Meta Business Verification, and a non-trivial encryption protocol (below).

### Encryption

Flows with dynamic data require a **Data Exchange endpoint**, and Meta only talks to it encrypted: RSA-OAEP (SHA-256) to unwrap an AES-128 key, then AES-128-GCM for the actual payload, with every bit of the request's IV flipped to derive the response's IV. `WhatsAppFlowEncryptionService` implements this using **BouncyCastle**, not .NET's built-in `AesGcm` - confirmed by direct testing that .NET's class only accepts a 12-byte nonce, while Meta's own IV doesn't reliably fit that constraint.

Two other encryption-specific lessons from testing against Meta directly:
- The encryption **key registration** endpoint (`POST /{phone-number-id}/whatsapp_business_encryption`) is scoped to the **phone number**, not the WABA - the only Flows-related endpoint that differs from the rest.
- A `421` response from the Data Exchange endpoint is Meta's own signal that decryption failed (stale/wrong key) - it's not a generic error code, it's how the protocol expects failures to be communicated.

### Stateless multi-screen navigation

A Flow with several screens needs each screen's answers carried into the next one. Rather than keep server-side session state, `FlowJsonCompiler` (at design time) wires every screen's Footer to forward *every* answer collected so far as part of its `navigate` payload (`{form.X}` for the screen's own fields, `{data.X}` for anything carried in from earlier screens). This means the Data Exchange endpoint's runtime logic is simple and stateless: whatever Meta sends us on a `data_exchange` request already contains every prior answer - we just add the next screen's own dynamic option data (e.g. the gym's class list) and pass the rest straight through.

### The Flow Designer

A single unified Portal page replaced three earlier, separate pages (list / structured editor / raw JSON editor) after early user testing showed the split was confusing. It now offers:
- A **Design mode**: build screens and components (heading, text, input, dropdown, checkbox group, radio group, footer) with no code, including a live illustrative preview of whichever element is selected.
- A **JSON mode**: edit the compiled Flow JSON directly, or upload one from a `.json` file - for anyone who prefers working closer to Meta's own tooling.
- A shared live preview panel that renders the *actual* screen being built, regardless of which mode produced it.
- Endpoint configuration and publishing on the same screen, since they're always done together in practice.

Two Meta-specific validation rules only discovered by testing live (now enforced client-side before saving, to avoid a confusing round trip through Meta's own validator): screen ids must contain only letters and underscores (no digits), and at least one screen must be marked both `terminal: true` **and** `success: true` — two separate properties Meta requires together.

## WABA (WhatsApp Business Account) Management

Setting a gym's WABA id (`POST /api/gyms/{gymId}/whatsapp-business-account`) automatically calls `POST /{waba-id}/subscribed_apps` on Meta's behalf - the "missing link" step that otherwise has to be done by hand in Graph API Explorer for every new gym, and was a recurring source of "messages just don't arrive" issues before it was automated.

## Compliance Dashboard

Pulls together everything above into one view: the phone number's live `quality_rating` and messaging tier (queried directly from Meta), our own error history in three separate categories (Meta-reported delivery failures, our own API call failures, AI reply failures), and computed risk flags - most notably, any active loyalty campaign still sending free text instead of an approved template.
