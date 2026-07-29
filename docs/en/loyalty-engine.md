# Loyalty Engine

## Objective

Improve member retention and engagement by automatically reaching out at the moments that matter (joining, birthdays, going quiet), without requiring a gym operator to remember to do it manually.

## Core Model: Message + Trigger Rule

A `Campaign` is deliberately just two things bolted together: a message template and a rule for when it fires. Four types, each interpreting the shared `TriggerDayOffset` field differently:

| Type | Trigger rule |
|---|---|
| `Welcome` | `TriggerDayOffset` days after a member joins (once per member) |
| `Birthday` | On the member's birthday, every year (`TriggerDayOffset` unused) |
| `Reactivation` | Once a member has been inactive for `TriggerDayOffset` days |
| `Manual` | Never fires automatically — an operator picks recipients and triggers it by hand from the Portal |

`MessageTemplate` uses `{FirstName}`, `{FullName}`, `{GymName}` placeholders, resolved per-recipient by `MessageTemplateRenderer` at send time.

## Idempotency

The single hardest problem in a scheduled messaging system is accidentally sending the same message twice. `CampaignMessage` exists purely to prevent that: one immutable record per `(campaign, member, period)` combination. Before dispatching, `LoyaltyEngineHandler` checks whether a `CampaignMessage` already exists for that exact combination; if it does, it skips silently. "Period" is trivial for Welcome (fires once, ever, per member) and for Birthday (once per calendar year), and is the evaluation date itself for Reactivation.

## Execution

`LoyaltyEngineBackgroundService` (an `IHostedService`) runs 15 seconds after the API starts, then every 24 hours. On each run, it iterates every active gym, evaluates each of its active automatic campaigns (`Welcome`/`Birthday`/`Reactivation`) against the gym's members, and dispatches whatever is due. `Manual` campaigns are never touched by this loop — they only fire via `POST /api/campaigns/{id}/trigger`, called from the Portal's Campaigns page after an operator selects specific members.

## Compliant Sending: Templates Over Free Text

This is the area where the loyalty engine intersects directly with WhatsApp's policy rules (see `whatsapp-integration.md`). A loyalty message is **business-initiated** — nothing guarantees the recipient messaged the gym within the last 24 hours, which is the only window Meta allows free-form text in. Sending free text outside that window risks rejection or, worse, damage to the phone number's quality rating.

`Campaign.WhatsAppMessageTemplateId` is how this gets resolved: link a campaign to an **Approved** `WhatsAppMessageTemplate`, and `LoyaltyEngineHandler` sends through `SendTemplateMessageAsync` instead of free text - resolving the template's declared variables (in order) from the same `{FirstName}`/`{FullName}`/`{GymName}` values the free-text renderer would have used. If a campaign isn't linked yet, or is linked to a template that hasn't been Approved, the handler logs a warning and falls back to free text - the campaign still sends (nothing silently breaks), but the Compliance Dashboard's risk flags will keep naming that campaign until it's properly linked.

## Visibility

The Portal's Campaigns page shows every campaign with its linked-template status, lets an operator activate/deactivate a campaign without deleting it, and (for Manual campaigns) lets them pick recipients from the gym's member list and trigger a send on demand. The Compliance Dashboard cross-references active campaigns against approved templates and calls out, by name, any campaign still sending free text.
