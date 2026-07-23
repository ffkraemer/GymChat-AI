using GymChatAI.Application.Abstractions;
using GymChatAI.Application.Common;
using GymChatAI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace GymChatAI.Application.Messaging;

/// <summary>
/// Orchestrates the end-to-end flow:
/// receive message -> resolve gym/conversation -> record it once -> [run the guided
/// onboarding/preferences menu, if applicable] -> otherwise ground with FAQs -> ask the AI
/// assistant -> persist -> send reply.
/// If the AI assistant fails, the message is queued as a PendingAIReply so
/// RetryPendingAIRepliesHandler can try again later instead of it being lost.
/// </summary>
public class ProcessIncomingMessageHandler
{
    private const int MaxContextMessages = 10;
    private const int MaxRelevantFaqs = 5;

    private readonly IGymRepository _gymRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IFaqRepository _faqRepository;
    private readonly ILeadRepository _leadRepository;
    private readonly IMemberRepository _memberRepository;
    private readonly IPendingAIReplyRepository _pendingAIReplyRepository;
    private readonly ILanguageDetector _languageDetector;
    private readonly IAIAssistantService _aiAssistantService;
    private readonly IWhatsAppMessageSender _whatsAppMessageSender;
    private readonly OnboardingFlowHandler _onboardingFlowHandler;
    private readonly GymChatAI.Application.Flows.WhatsAppFlowCompletionHandler _flowCompletionHandler;
    private readonly ILogger<ProcessIncomingMessageHandler> _logger;

    public ProcessIncomingMessageHandler(
        IGymRepository gymRepository,
        IConversationRepository conversationRepository,
        IFaqRepository faqRepository,
        ILeadRepository leadRepository,
        IMemberRepository memberRepository,
        IPendingAIReplyRepository pendingAIReplyRepository,
        ILanguageDetector languageDetector,
        IAIAssistantService aiAssistantService,
        IWhatsAppMessageSender whatsAppMessageSender,
        OnboardingFlowHandler onboardingFlowHandler,
        GymChatAI.Application.Flows.WhatsAppFlowCompletionHandler flowCompletionHandler,
        ILogger<ProcessIncomingMessageHandler> logger)
    {
        _gymRepository = gymRepository;
        _conversationRepository = conversationRepository;
        _faqRepository = faqRepository;
        _leadRepository = leadRepository;
        _memberRepository = memberRepository;
        _pendingAIReplyRepository = pendingAIReplyRepository;
        _languageDetector = languageDetector;
        _aiAssistantService = aiAssistantService;
        _whatsAppMessageSender = whatsAppMessageSender;
        _onboardingFlowHandler = onboardingFlowHandler;
        _flowCompletionHandler = flowCompletionHandler;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(IncomingWhatsAppMessage message, CancellationToken cancellationToken = default)
    {
        // 1. Idempotency: WhatsApp may redeliver webhooks.
        if (await _conversationRepository.ExistsByWhatsAppMessageIdAsync(message.WhatsAppMessageId, cancellationToken))
        {
            _logger.LogInformation("Message {WhatsAppMessageId} already processed, skipping.", message.WhatsAppMessageId);
            return Result.Success();
        }

        // 2. Resolve the gym that owns this WhatsApp number.
        var gym = await _gymRepository.GetByWhatsAppPhoneNumberIdAsync(message.WhatsAppPhoneNumberId, cancellationToken);
        if (gym is null)
        {
            _logger.LogWarning("No gym configured for WhatsApp phone number id {PhoneNumberId}.", message.WhatsAppPhoneNumberId);
            return Result.Failure("Unknown WhatsApp phone number id.");
        }

        // 3. Resolve or create the conversation for this contact.
        var conversation = await _conversationRepository.GetOpenByContactAsync(gym.Id, message.FromPhoneNumber, cancellationToken);
        var isNewConversation = conversation is null;
        if (conversation is null)
        {
            conversation = new Conversation(gym.Id, message.FromPhoneNumber);
            await EnsureLeadCapturedAsync(gym.Id, message, cancellationToken);
        }

        // 4. Detect language (nothing to detect from a button/list tap - keep whatever the
        // conversation already had) and record the inbound message exactly once.
        var language = string.IsNullOrEmpty(message.Text)
            ? conversation.PreferredLanguage
            : await _languageDetector.DetectAsync(message.Text, cancellationToken);

        conversation.AddInboundMessage(GetLoggableContent(message), message.WhatsAppMessageId, language);

        if (isNewConversation)
            await _conversationRepository.AddAsync(conversation, cancellationToken);

        // 4b. A completed WhatsApp Flow submission (the native form) - handle it directly,
        // regardless of new/existing conversation or menu state, and stop there.
        if (message.FlowResponseJson is not null)
        {
            await _flowCompletionHandler.HandleAsync(gym.Id, message.FromPhoneNumber, message.FlowResponseJson, cancellationToken);
            if (!isNewConversation)
                await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            return Result.Success();
        }

        // 5. Brand new contact: lead with the onboarding menu instead of handing their
        // first message straight to the AI - captures consent + preferences up front.
        if (isNewConversation)
        {
            await _onboardingFlowHandler.StartOnboardingAsync(gym, conversation, cancellationToken);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            return Result.Success();
        }

        // 6. Mid-menu (a button/list reply is expected) - let the flow handler take it instead of the AI.
        if (conversation.FlowStep != Domain.Enums.ConversationFlowStep.None)
        {
            await _onboardingFlowHandler.TryHandleFlowMessageAsync(gym, conversation, message, cancellationToken);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            return Result.Success();
        }

        // 7. Anyone can revisit their notification preferences at any time with a keyword.
        if (string.IsNullOrWhiteSpace(message.InteractiveReplyId) && OnboardingFlowHandler.IsPreferencesKeyword(message.Text))
        {
            await _onboardingFlowHandler.RestartAsync(gym, conversation, cancellationToken);
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);
            return Result.Success();
        }

        // 8. Ordinary message: ground with FAQs and hand off to the AI assistant.
        var relevantFaqs = await _faqRepository.SearchAsync(gym.Id, message.Text, MaxRelevantFaqs, cancellationToken);

        var context = new AIAssistantContext(
            GymName: gym.Name,
            PreferredLanguage: conversation.PreferredLanguage,
            History: conversation.GetRecentContext(MaxContextMessages)
                .Select(m => new AIConversationTurn(m.Direction == Domain.Enums.MessageDirection.Inbound ? "user" : "assistant", m.Content))
                .ToList(),
            RelevantFaqs: relevantFaqs.Select(f => (f.Question, f.Answer)).ToList());

        string replyText;
        try
        {
            replyText = await _aiAssistantService.GenerateReplyAsync(context, message.Text, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI assistant failed to generate a reply for conversation {ConversationId}.", conversation.Id);
            conversation.EscalateToHuman();
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);

            // Don't let the question vanish into the void: queue it so
            // RetryPendingAIRepliesHandler tries again once the provider recovers.
            var pendingReply = new PendingAIReply(conversation.Id, gym.Id, message.Text);
            await _pendingAIReplyRepository.AddAsync(pendingReply, cancellationToken);

            return Result.Failure("AI assistant unavailable - queued for retry.");
        }

        var outboundMessage = conversation.AddOutboundMessage(replyText, Domain.Enums.MessageOrigin.AiAssistant);

        try
        {
            var wamid = await _whatsAppMessageSender.SendTextMessageAsync(
                gym.WhatsAppPhoneNumberId, message.FromPhoneNumber, replyText, cancellationToken);
            outboundMessage.MarkSent(wamid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send WhatsApp reply for conversation {ConversationId}.", conversation.Id);
            outboundMessage.MarkFailed();
        }

        await _conversationRepository.UpdateAsync(conversation, cancellationToken);
        return Result.Success();
    }

    private async Task EnsureLeadCapturedAsync(Guid gymId, IncomingWhatsAppMessage message, CancellationToken cancellationToken)
    {
        var existingMember = await _memberRepository.GetByPhoneAsync(gymId, message.FromPhoneNumber, cancellationToken);
        if (existingMember is not null) return; // Already a member, no need to capture as a lead.

        var existingLead = await _leadRepository.GetByPhoneAsync(gymId, message.FromPhoneNumber, cancellationToken);
        if (existingLead is not null) return;

        var lead = new Lead(gymId, message.FromPhoneNumber, message.ContactName);
        await _leadRepository.AddAsync(lead, cancellationToken);
    }

    /// <summary>
    /// Message.Content can never be empty (a domain invariant), but a button/list tap
    /// arrives with Text = "" - WhatsApp only sends the id of whichever option was chosen,
    /// no free text. Falls back to a readable placeholder for the history in that case.
    /// </summary>
    private static string GetLoggableContent(IncomingWhatsAppMessage message) =>
        !string.IsNullOrEmpty(message.Text) ? message.Text : $"[Menu: {message.InteractiveReplyId}]";
}
