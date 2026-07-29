# Domain Model

## Main Entities

### Gym
Represents a gym (the platform's tenant). Owns exactly one WhatsApp phone number (`WhatsAppPhoneNumberId`) and, optionally, one WhatsApp Business Account id (`WhatsAppBusinessAccountId`) — required before templates or Flows can be managed for that gym. `IsActive` supports deactivation without deleting historical data.

### Member
A registered gym member. Tracked by `FullName`, `PhoneNumber`, `BirthDate` (drives the Birthday campaign), and `Status` (Active/Inactive/Suspended/Cancelled). `FirstName` is a computed property (the first word of `FullName`), never stored separately.

### Lead
A contact who has messaged the gym but isn't (yet) a member. Captured automatically the first time an unknown phone number writes in, with a `Status` funnel (New → Contacted → Qualified → Converted/Lost).

### Conversation
One WhatsApp conversation with a contact (lead or member). Holds `Status` (Open/WaitingForHuman/Closed), `PreferredLanguage`, and the full `Messages` history. `EscalateToHuman()`/`ResolveEscalation()` mark when the AI failed and a human needs to step in.

### Message
One inbound or outbound message within a Conversation. `Direction` (Inbound/Outbound), `Origin` (Human/AiAssistant/System), and `Status` (Received/Processing/Sent/Delivered/Read/Failed). `Content` can never be empty — a domain invariant that surfaced a real bug early on: a WhatsApp button/list tap carries no free text, only an interactive reply id, so a placeholder string is used for those instead of the raw (empty) text.

### Campaign
A loyalty message + its trigger rule. `Type` (Welcome/Birthday/Reactivation/Manual) decides how `TriggerDayOffset` is interpreted (days after joining, days of inactivity, or unused for Birthday/Manual). `MessageTemplate` uses `{FirstName}`/`{FullName}`/`{GymName}` placeholders. Optionally linked (`WhatsAppMessageTemplateId`) to an **Approved** `WhatsAppMessageTemplate` — required for the campaign to send compliantly outside the 24h customer-service window; without it, `LoyaltyEngineHandler` falls back to free text (and the Compliance Dashboard flags it).

### CampaignMessage
An immutable dispatch record — one row per (campaign, member, period) combination, existing purely so `LoyaltyEngineHandler` never sends the same campaign to the same member twice within the same period. `Status` (Pending/Sent/Failed).

### Faq
One question/answer pair in the knowledge base the AI grounds its answers with, tagged by `Category`. Soft-deletable (`IsActive`).

### Plan
A membership plan (name, description, price) shown to prospects who ask about pricing.

### Promotion
A time-bounded promotional offer (`StartDate`/`EndDate`) the AI can reference when relevant.

### ClassType
A category of class a gym offers (e.g. "Yoga", "Spinning") - admin-configurable per gym. Feeds the dynamic options in both the older button/list onboarding menu and WhatsApp Flows (`GymClassTypes` as a Flow option source).

### NotificationPreference
One contact's opted-in class-notification settings per gym: whether they've completed onboarding, opted in, which `ClassType`s they picked, and a list of `NotificationTimeSlot`s (day + time window). `ResetSelections()` clears everything for a fresh run-through of the preferences flow.

### NotificationTimeSlot
A single (day of week, time window) pair a contact wants notifications for. Owned exclusively by `NotificationPreference` - never referenced independently.

### PendingAIReply
Tracks a message the AI failed to answer (provider outage, rate limit), so `PendingAIReplyBackgroundService` can retry it later instead of the question vanishing silently. `Status` (Pending/Resolved/Abandoned), capped at 5 attempts.

### WhatsAppApiError
An audit record of *our own* failed calls to the Graph API (auth failures, malformed requests, rate limits) — feeds the Compliance Dashboard's "API call failures" panel.

### WhatsAppDeliveryFailure
An audit record of a delivery failure *Meta itself* reported, after the fact, via the webhook's `statuses` field - distinct from `WhatsAppApiError` because it represents a message we successfully sent that still couldn't reach the recipient (e.g. the number isn't on WhatsApp, or blocked the business).

### WhatsAppMessageTemplate
A Meta message template, manageable from the Administration Portal instead of Meta Business Manager. Uses the same `{VariableName}` placeholder syntax as `Campaign.MessageTemplate`, translated to Meta's positional `{{1}}`, `{{2}}`... syntax only at submission time. Tracks both the `Category` we submitted and the `ActualCategory` Meta assigned after review (which can differ - Meta silently reclassifies templates). `Status` (Draft/PendingApproval/Approved/Rejected/Paused/Disabled); once submitted, the body becomes immutable (a new template has to be created instead of edited).

### WhatsAppFlow
A WhatsApp Flow (Meta's native multi-screen form). Owns a collection of `FlowScreen`s (the editable source of truth, built via the Portal's Flow Designer) plus the compiled `FlowJson` actually sent to Meta. Tracks a snapshot of the gym's `WhatsAppBusinessAccountId` at creation time, so the Portal can hide Flows left over from a WABA the gym has since moved away from, without deleting them.

### FlowScreen
One screen of a Flow - a container of `FlowComponent`s, always ending with exactly one Footer. `ScreenId` must contain only letters and underscores (a Meta constraint enforced both client-side and in the domain constructor).

### FlowComponent
One field/element on a screen: heading, body text, text input, dropdown, checkbox group, radio button group, or footer. Input components carry a `VariableName` (the key under which the answer appears in the final submission) and, for choice components, an `OptionsSource` (static list typed by the admin, or a dynamic source: `GymClassTypes`/`DaysOfWeek`, resolved live by the Flow's Data Exchange endpoint). A Footer component's `FooterAction` (Navigate/Complete) determines whether it moves to another screen or ends the Flow.

### ApplicationUser (Identity)
An Administration Portal login (`IdentityUser<Guid>` + `GymId` + `FullName`). Two roles: `Admin` (scoped to one gym via `GymScopeFilter`) and `PlatformAdmin` (a cross-tenant role with no gym of its own - `GymId = Guid.Empty` as a sentinel - used to onboard new gyms and manage cross-tenant configuration like WABA ids and encryption keys on any gym's behalf).
