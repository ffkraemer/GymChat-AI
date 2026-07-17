using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GymChatAI.Application.Messaging;

/// <summary>
/// Retries messages queued in PendingAIReply after the AI provider failed on the first
/// attempt (see ProcessIncomingMessageHandler). Designed to be called periodically by a
/// scheduler (see PendingAIReplyBackgroundService in Infrastructure) - every few minutes is
/// enough, since these are customer-facing replies, not daily campaigns.
/// </summary>
public class RetryPendingAIRepliesHandler
{
    private const int MaxRelevantFaqs = 5;
    private const int MaxContextMessages = 10;
    private const int MaxAttempts = 5;

    private readonly IPendingAIReplyRepository _pendingAIReplyRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IGymRepository _gymRepository;
    private readonly IFaqRepository _faqRepository;
    private readonly IAIAssistantService _aiAssistantService;
    private readonly IWhatsAppMessageSender _whatsAppMessageSender;
    private readonly ILogger<RetryPendingAIRepliesHandler> _logger;

    public RetryPendingAIRepliesHandler(
        IPendingAIReplyRepository pendingAIReplyRepository,
        IConversationRepository conversationRepository,
        IGymRepository gymRepository,
        IFaqRepository faqRepository,
        IAIAssistantService aiAssistantService,
        IWhatsAppMessageSender whatsAppMessageSender,
        ILogger<RetryPendingAIRepliesHandler> logger)
    {
        _pendingAIReplyRepository = pendingAIReplyRepository;
        _conversationRepository = conversationRepository;
        _gymRepository = gymRepository;
        _faqRepository = faqRepository;
        _aiAssistantService = aiAssistantService;
        _whatsAppMessageSender = whatsAppMessageSender;
        _logger = logger;
    }

    public async Task ProcessPendingRepliesAsync(CancellationToken cancellationToken = default)
    {
        var pendingReplies = await _pendingAIReplyRepository.GetPendingAsync(cancellationToken);
        if (pendingReplies.Count == 0) return;

        _logger.LogInformation("Retrying {Count} pending AI repl{Suffix}.", pendingReplies.Count, pendingReplies.Count == 1 ? "y" : "ies");

        foreach (var pending in pendingReplies)
        {
            await RetryOneAsync(pending, cancellationToken);
        }
    }

    private async Task RetryOneAsync(Domain.Entities.PendingAIReply pending, CancellationToken cancellationToken)
    {
        var conversation = await _conversationRepository.GetByIdAsync(pending.ConversationId, cancellationToken);
        var gym = await _gymRepository.GetByIdAsync(pending.GymId, cancellationToken);

        // The conversation or gym disappeared somehow - nothing sensible left to retry.
        if (conversation is null || gym is null)
        {
            pending.MarkResolved();
            await _pendingAIReplyRepository.UpdateAsync(pending, cancellationToken);
            return;
        }

        var relevantFaqs = await _faqRepository.SearchAsync(gym.Id, pending.UserMessage, MaxRelevantFaqs, cancellationToken);

        var context = new AIAssistantContext(
            GymName: gym.Name,
            PreferredLanguage: conversation.PreferredLanguage,
            History: conversation.GetRecentContext(MaxContextMessages)
                .Select(m => new AIConversationTurn(m.Direction == MessageDirection.Inbound ? "user" : "assistant", m.Content))
                .ToList(),
            RelevantFaqs: relevantFaqs.Select(f => (f.Question, f.Answer)).ToList());

        try
        {
            var replyText = await _aiAssistantService.GenerateReplyAsync(context, pending.UserMessage, cancellationToken);

            var outboundMessage = conversation.AddOutboundMessage(replyText, MessageOrigin.AiAssistant);
            var wamid = await _whatsAppMessageSender.SendTextMessageAsync(gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber, replyText, cancellationToken);
            outboundMessage.MarkSent(wamid);

            conversation.ResolveEscalation();
            await _conversationRepository.UpdateAsync(conversation, cancellationToken);

            pending.MarkResolved();
            await _pendingAIReplyRepository.UpdateAsync(pending, cancellationToken);

            _logger.LogInformation("Resolved pending AI reply for conversation {ConversationId} after {Attempts} failed attempt(s).", conversation.Id, pending.Attempts);
        }
        catch (Exception ex)
        {
            pending.RecordFailedAttempt(ex.Message, MaxAttempts);
            await _pendingAIReplyRepository.UpdateAsync(pending, cancellationToken);

            if (pending.Status == Domain.Enums.PendingAIReplyStatus.Abandoned)
                _logger.LogWarning("Giving up on pending AI reply for conversation {ConversationId} after {Attempts} attempts.", conversation.Id, pending.Attempts);
            else
                _logger.LogWarning(ex, "Retry {Attempts} failed for pending AI reply, conversation {ConversationId}.", pending.Attempts, conversation.Id);
        }
    }
}
