# AI Architecture

## Components

- **Three interchangeable providers**: Gemini, OpenAI, and Azure OpenAI (not a single fixed model)
- **FAQ-based grounding** (not a formal RAG pipeline)
- **No tool calling / function calling**
- **Multi-language** response, driven by a lightweight language detector

## Interchangeable Providers, Not a Single Model

The AI assistant sits behind one port, `IAIAssistantService`, with three implementations: `GeminiAIAssistantService`, `OpenAIAssistantService`, `AzureOpenAIAssistantService`. Which one answers a given message is decided entirely by configuration - either an explicit `AiProvider` setting, or auto-detected from whichever provider's API key is populated (checked in order: Gemini, then OpenAI, then falling back to Azure OpenAI). None of the conversation logic, FAQ grounding, or language handling knows or needs to know which provider is active.

This is a direct response to two things encountered during development, not a theoretical preference:
- **Provider-side churn**: Gemini's model naming changed mid-project (`gemini-pro` was deprecated in favor of `gemini-flash-latest`), and Google, OpenAI, and Microsoft each have historically made breaking changes to their APIs on their own schedule, outside this project's control.
- **Rate limits and outages**: a provider hiccup shouldn't turn into an emergency deploy - just a configuration change, or an automatic retry (see "Reliability" below).

## Grounding: FAQ Search, Not RAG

There is **no** vector database, no embeddings, and no retrieval-augmented-generation pipeline in the formal sense. Grounding works like this instead:

1. `IFaqRepository.SearchAsync` runs a basic text-relevance search over the gym's FAQ entries for the incoming message.
2. The top matches (capped at 5) are injected directly into the prompt as `(question, answer)` pairs, alongside the gym's name and the recent conversation history (last 10 messages).
3. The AI answers using that context - it never queries anything itself.

This is a deliberate simplification, not an oversight: a gym's knowledge base is a handful of FAQs, plans, and promotions - small enough that a plain relevance search is both cheap and sufficiently accurate, without needing the operational overhead of a vector store. If the knowledge base grows substantially (e.g. hundreds of documents per gym), formal RAG would become worth revisiting.

## No Tool Calling

The AI is never given function/tool-calling capability, and never decides *what our system does* - it only decides *what to say*. Every piece of flow logic (starting onboarding, advancing a button/list menu, running a WhatsApp Flow, triggering a loyalty campaign) is decided by our own Application-layer code, either before the AI is consulted at all (the common case: onboarding, menus, and Flows all intercept the message before it ever reaches the AI) or is entirely unrelated to it (the loyalty engine's background service). This keeps the AI's blast radius limited to "generate a text reply grounded in known facts" - it can't accidentally trigger a side effect.

## Multi-Language

`ILanguageDetector` (implemented by `HeuristicLanguageDetector`) inspects each inbound message's text and classifies it as Portuguese, English, Spanish, or Unknown. The detected language is stored on the `Conversation` (`PreferredLanguage`) and passed to the AI as part of its context, so replies stay in the language the contact is actually writing in, even mid-conversation if they switch.

## Reliability

If the active provider fails outright (outage, rate limit, invalid key), the failure is caught, the conversation is marked `EscalateToHuman()`, and the original question is stored as a `PendingAIReply`. `PendingAIReplyBackgroundService` retries it automatically every 3 minutes, up to 5 attempts, before giving up and leaving the conversation escalated. This means a transient provider issue delays a reply rather than losing the question entirely.

`GET /health/ai` reports whether the active provider's credentials are currently valid, surfacing key expiry/revocation problems directly instead of requiring a live debugging session every time a reply silently fails to generate.
