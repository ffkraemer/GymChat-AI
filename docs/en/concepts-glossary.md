# Concepts Glossary — Campaigns, Templates, and Flows

These three concepts get confused often because they all touch "sending something to a contact via WhatsApp" — but each solves a different problem, and they combine rather than replace each other.

## The One-Line Version

| | What it is | What it answers |
|---|---|---|
| **Template** | The approved *text* a business-initiated message is allowed to use | *"What am I allowed to say?"* |
| **Campaign** | A message + a rule for *when* to send it | *"When do I say it?"* |
| **Flow** | A native multi-screen *form* for collecting structured answers | *"How do I collect a structured answer, instead of just sending text?"* |

None of them work alone in every situation - each depends on something else to actually reach a contact meaningfully.

## Message Template

**What it's for:** Meta only allows free-form text within an open 24h customer-service window (the contact messaged you recently). Outside that window, a **business-initiated** message must use a pre-approved template, or risk rejection / damage to the number's quality rating. A Template is that pre-approved text, managed from the Portal instead of Meta Business Manager.

**On its own:** A Template sitting unused does nothing - it's just approved text waiting to be sent by something.

**Lifecycle:** Draft → submit for Meta's review → Approved/Rejected/Paused. Once submitted, the body can't be edited - a new Template has to be created instead.

## Campaign

**What it's for:** Automating *when* a message goes out, without a human having to remember and act on it. Four types: `Welcome` (X days after joining), `Birthday` (every year), `Reactivation` (after X days inactive), `Manual` (an operator triggers it by hand, for chosen recipients).

**On its own:** A Campaign can send free text - but only safely if it's likely to land within an open 24h window, which loyalty messages (business-initiated, not a reply to something the contact just said) generally aren't.

**What "linking to a Template" means:** Instead of sending its own free-text body, the Campaign sends *through* an Approved Template - the Template supplies the compliant, pre-approved wording; the Campaign still supplies the trigger rule and the per-recipient values (`{FirstName}`, `{GymName}`, etc.). A Campaign without a linked Template still works, but is exactly the situation the Compliance Dashboard flags as a risk.

## WhatsApp Flow

**What it's for:** Collecting a *structured* answer - multiple choice, multi-select, a form with several fields - as a native WhatsApp screen, instead of a chain of button/list taps or free text the AI has to parse. Used here for notification preferences (which classes, which day, which time window).

**On its own:** A Flow has to be *triggered* by something - a message with a "open this Flow" button, sent either manually (from the Portal, for testing) or as part of some other logic you build (e.g. a Campaign or the onboarding menu could send a Flow-trigger message instead of a plain button menu, though that wiring isn't automatic - it has to be built for the specific case).

**Static vs Dynamic:** A Flow whose questions/options are entirely fixed at design time is *static* - no extra setup needed. A Flow that needs live data (e.g. the gym's current class list) is *dynamic* - it must be marked as such, and needs a Data Exchange endpoint URL configured before it can publish (see `whatsapp-integration.md`).

## How They Actually Combine

A realistic example: a **Reactivation Campaign** (fires after 30 days inactive) is linked to an **Approved Template** ("Sentimos a tua falta, {FirstName}!") so it can compliantly reach someone outside the 24h window. That message could, in turn, contain a call-to-action that leads the contact to open a **Flow** to update their class preferences. Three different concepts, three different jobs, working together - not three names for the same thing.
