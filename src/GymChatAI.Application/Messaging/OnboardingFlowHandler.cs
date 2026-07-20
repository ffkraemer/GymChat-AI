using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Entities;
using GymChatAI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GymChatAI.Application.Messaging;

/// <summary>
/// Drives the guided WhatsApp menu: onboarding consent on first contact, then (if the
/// person opts in) which class types and which day/time windows they want suggestions for.
/// WhatsApp has no native concept of "where a user is in a form", so Conversation.FlowStep
/// plays that role - this handler reads it, asks the next question, and advances it.
///
/// Called from ProcessIncomingMessageHandler in two situations:
/// 1. A brand new conversation - <see cref="StartOnboardingAsync"/> kicks off the menu
///    before anything is handed to the AI assistant.
/// 2. An existing conversation with FlowStep != None (mid-menu), or someone typing the
///    "preferências" keyword to revisit their choices - <see cref="TryHandleFlowMessageAsync"/>.
/// </summary>
public class OnboardingFlowHandler
{
    private const string PreferencesKeyword = "preferências";
    private const string PreferencesKeywordAscii = "preferencias";

    private readonly INotificationPreferenceRepository _preferenceRepository;
    private readonly IClassTypeRepository _classTypeRepository;
    private readonly IWhatsAppMessageSender _whatsAppMessageSender;
    private readonly ILogger<OnboardingFlowHandler> _logger;

    public OnboardingFlowHandler(
        INotificationPreferenceRepository preferenceRepository,
        IClassTypeRepository classTypeRepository,
        IWhatsAppMessageSender whatsAppMessageSender,
        ILogger<OnboardingFlowHandler> logger)
    {
        _preferenceRepository = preferenceRepository;
        _classTypeRepository = classTypeRepository;
        _whatsAppMessageSender = whatsAppMessageSender;
        _logger = logger;
    }

    public static bool IsPreferencesKeyword(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        return normalized == PreferencesKeyword || normalized == PreferencesKeywordAscii;
    }

    /// <summary>Sends the initial consent question to a brand new contact.</summary>
    public async Task StartOnboardingAsync(Gym gym, Conversation conversation, CancellationToken cancellationToken)
    {
        var (preference, isNew) = await GetOrCreatePreferenceAsync(gym.Id, conversation.ContactPhoneNumber, cancellationToken);

        await SendOnboardingConsentButtonsAsync(
            gym, conversation,
            $"Bem-vindo(a) ao {gym.Name}! 👋 Queres receber sugestões de aulas por WhatsApp, no dia e hora que preferires?",
            cancellationToken);

        conversation.SetFlowStep(ConversationFlowStep.AwaitingOnboardingConsent);
        await PersistPreferenceAsync(preference, isNew, cancellationToken);
    }

    /// <summary>
    /// Restarts the menu on demand (the "preferências" keyword), even for someone who
    /// already finished onboarding before - lets them revisit their choices.
    /// </summary>
    public async Task RestartAsync(Gym gym, Conversation conversation, CancellationToken cancellationToken)
    {
        var (preference, isNew) = await GetOrCreatePreferenceAsync(gym.Id, conversation.ContactPhoneNumber, cancellationToken);
        preference.ResetSelections();
        await PersistPreferenceAsync(preference, isNew, cancellationToken);

        await SendOnboardingConsentButtonsAsync(
            gym, conversation,
            "Vamos rever as tuas preferências de notificações. Queres continuar a receber sugestões de aulas?",
            cancellationToken);

        conversation.SetFlowStep(ConversationFlowStep.AwaitingOnboardingConsent);
    }

    /// <summary>
    /// Advances a mid-flow conversation based on the button/list id the person just tapped.
    /// Returns false if there was nothing to handle (e.g. FlowStep is None) - the caller
    /// should fall through to the normal AI-assistant flow in that case.
    /// </summary>
    public async Task<bool> TryHandleFlowMessageAsync(Gym gym, Conversation conversation, IncomingWhatsAppMessage message, CancellationToken cancellationToken)
    {
        if (conversation.FlowStep == ConversationFlowStep.None)
            return false;

        var replyId = message.InteractiveReplyId;
        if (string.IsNullOrWhiteSpace(replyId))
        {
            // They typed free text instead of tapping a button/row - this often happens
            // because the original menu message went "stale" (WhatsApp disables a button
            // once tapped, even if our server failed to process that tap in time). Resend
            // a fresh, tappable version of whatever step they're on, instead of just asking
            // them to use a menu that may no longer be usable.
            await _whatsAppMessageSender.SendTextMessageAsync(
                gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber,
                "Usa uma das opções abaixo para continuar 🙂", cancellationToken);
            await ResendCurrentStepMenuAsync(gym, conversation, cancellationToken);
            return true;
        }

        var (preference, isNewPreference) = await GetOrCreatePreferenceAsync(gym.Id, conversation.ContactPhoneNumber, cancellationToken);

        switch (conversation.FlowStep)
        {
            case ConversationFlowStep.AwaitingOnboardingConsent:
                await HandleOnboardingConsentAsync(gym, conversation, preference, isNewPreference, replyId, cancellationToken);
                break;

            case ConversationFlowStep.AwaitingClassTypeSelection:
                await HandleClassTypeSelectionAsync(gym, conversation, preference, isNewPreference, replyId, cancellationToken);
                break;

            case ConversationFlowStep.AwaitingMoreClassTypesDecision:
                await HandleMoreClassTypesDecisionAsync(gym, conversation, replyId, cancellationToken);
                break;

            case ConversationFlowStep.AwaitingDaySelection:
                await HandleDaySelectionAsync(gym, conversation, replyId, cancellationToken);
                break;

            case ConversationFlowStep.AwaitingTimeWindowSelection:
                await HandleTimeWindowSelectionAsync(gym, conversation, preference, isNewPreference, replyId, cancellationToken);
                break;

            case ConversationFlowStep.AwaitingMoreSlotsDecision:
                await HandleMoreSlotsDecisionAsync(gym, conversation, preference, replyId, cancellationToken);
                break;
        }

        return true;
    }

    /// <summary>Re-sends whichever button/list menu belongs to the conversation's current step, unchanged.</summary>
    private async Task ResendCurrentStepMenuAsync(Gym gym, Conversation conversation, CancellationToken ct)
    {
        switch (conversation.FlowStep)
        {
            case ConversationFlowStep.AwaitingOnboardingConsent:
                await SendOnboardingConsentButtonsAsync(gym, conversation, "Queres receber sugestões de aulas?", ct);
                break;

            case ConversationFlowStep.AwaitingClassTypeSelection:
                await SendClassTypeListAsync(gym, conversation, ct);
                break;

            case ConversationFlowStep.AwaitingMoreClassTypesDecision:
                await SendMoreClassTypesButtonsAsync(gym, conversation, ct);
                break;

            case ConversationFlowStep.AwaitingDaySelection:
                await SendDayListAsync(gym, conversation, ct);
                break;

            case ConversationFlowStep.AwaitingTimeWindowSelection:
                await SendTimeWindowButtonsAsync(gym, conversation, ct);
                break;

            case ConversationFlowStep.AwaitingMoreSlotsDecision:
                await SendMoreSlotsButtonsAsync(gym, conversation, ct);
                break;
        }
    }

    private async Task HandleOnboardingConsentAsync(Gym gym, Conversation conversation, NotificationPreference preference, bool isNewPreference, string replyId, CancellationToken ct)
    {
        if (replyId == "onboarding_no")
        {
            preference.CompleteOnboarding(optedIn: false);
            await PersistPreferenceAsync(preference, isNewPreference, ct);

            await _whatsAppMessageSender.SendTextMessageAsync(
                gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber,
                "Sem problema! Podes ativar isto a qualquer momento escrevendo \"preferências\".", ct);

            conversation.SetFlowStep(ConversationFlowStep.None);
            return;
        }

        preference.CompleteOnboarding(optedIn: true);
        await PersistPreferenceAsync(preference, isNewPreference, ct);
        await SendClassTypeListAsync(gym, conversation, ct);
        conversation.SetFlowStep(ConversationFlowStep.AwaitingClassTypeSelection);
    }

    private async Task HandleClassTypeSelectionAsync(Gym gym, Conversation conversation, NotificationPreference preference, bool isNewPreference, string replyId, CancellationToken ct)
    {
        if (!Guid.TryParse(replyId, out var classTypeId))
        {
            await SendClassTypeListAsync(gym, conversation, ct);
            return;
        }

        preference.SelectClassType(classTypeId);
        await PersistPreferenceAsync(preference, isNewPreference, ct);
        await SendMoreClassTypesButtonsAsync(gym, conversation, ct);
        conversation.SetFlowStep(ConversationFlowStep.AwaitingMoreClassTypesDecision);
    }

    private async Task HandleMoreClassTypesDecisionAsync(Gym gym, Conversation conversation, string replyId, CancellationToken ct)
    {
        if (replyId == "classes_add_more")
        {
            await SendClassTypeListAsync(gym, conversation, ct);
            conversation.SetFlowStep(ConversationFlowStep.AwaitingClassTypeSelection);
            return;
        }

        await SendDayListAsync(gym, conversation, ct);
        conversation.SetFlowStep(ConversationFlowStep.AwaitingDaySelection);
    }

    private async Task HandleDaySelectionAsync(Gym gym, Conversation conversation, string replyId, CancellationToken ct)
    {
        if (!TryParseDayId(replyId, out _))
        {
            await SendDayListAsync(gym, conversation, ct);
            return;
        }

        await SendTimeWindowButtonsAsync(gym, conversation, ct);

        // Remember the chosen day until the time window arrives to pair it with.
        conversation.SetFlowStep(ConversationFlowStep.AwaitingTimeWindowSelection, pendingFlowData: replyId);
    }

    private async Task HandleTimeWindowSelectionAsync(Gym gym, Conversation conversation, NotificationPreference preference, bool isNewPreference, string replyId, CancellationToken ct)
    {
        if (!TryParseDayId(conversation.PendingFlowData, out var dayOfWeek) || !TryParseWindowId(replyId, out var window))
        {
            // Lost/invalid mid-flow state - safest recovery is to just restart the day step.
            await SendDayListAsync(gym, conversation, ct);
            conversation.SetFlowStep(ConversationFlowStep.AwaitingDaySelection);
            return;
        }

        preference.AddTimeSlot(dayOfWeek, window);
        await PersistPreferenceAsync(preference, isNewPreference, ct);
        await SendMoreSlotsButtonsAsync(gym, conversation, ct);
        conversation.SetFlowStep(ConversationFlowStep.AwaitingMoreSlotsDecision);
    }

    private async Task HandleMoreSlotsDecisionAsync(Gym gym, Conversation conversation, NotificationPreference preference, string replyId, CancellationToken ct)
    {
        if (replyId == "slots_add_more")
        {
            await SendDayListAsync(gym, conversation, ct);
            conversation.SetFlowStep(ConversationFlowStep.AwaitingDaySelection);
            return;
        }

        var summary = await BuildConfirmationSummaryAsync(preference, ct);
        await _whatsAppMessageSender.SendTextMessageAsync(gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber, summary, ct);

        conversation.SetFlowStep(ConversationFlowStep.None);
    }

    private async Task SendOnboardingConsentButtonsAsync(Gym gym, Conversation conversation, string bodyText, CancellationToken ct) =>
        await _whatsAppMessageSender.SendButtonMessageAsync(
            gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber, bodyText,
            [new WhatsAppButtonOption("onboarding_yes", "Sim, quero!"), new WhatsAppButtonOption("onboarding_no", "Agora não")],
            ct);

    private async Task SendMoreClassTypesButtonsAsync(Gym gym, Conversation conversation, CancellationToken ct) =>
        await _whatsAppMessageSender.SendButtonMessageAsync(
            gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber,
            "Queres escolher mais algum tipo de aula?",
            [new WhatsAppButtonOption("classes_add_more", "Sim, outra"), new WhatsAppButtonOption("classes_done", "Não, continuar")],
            ct);

    private async Task SendTimeWindowButtonsAsync(Gym gym, Conversation conversation, CancellationToken ct) =>
        await _whatsAppMessageSender.SendButtonMessageAsync(
            gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber,
            "E em que altura do dia?",
            [
                new WhatsAppButtonOption("window_morning", "Manhã"),
                new WhatsAppButtonOption("window_afternoon", "Tarde"),
                new WhatsAppButtonOption("window_evening", "Noite")
            ],
            ct);

    private async Task SendMoreSlotsButtonsAsync(Gym gym, Conversation conversation, CancellationToken ct) =>
        await _whatsAppMessageSender.SendButtonMessageAsync(
            gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber,
            "Queres adicionar mais algum dia/horário?",
            [new WhatsAppButtonOption("slots_add_more", "Sim, outro"), new WhatsAppButtonOption("slots_done", "Não, terminar")],
            ct);

    private async Task SendClassTypeListAsync(Gym gym, Conversation conversation, CancellationToken ct)
    {
        var classTypes = await _classTypeRepository.GetActiveByGymAsync(gym.Id, ct);

        if (classTypes.Count == 0)
        {
            await _whatsAppMessageSender.SendTextMessageAsync(
                gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber,
                "Ainda não há tipos de aula configurados - avisamos assim que houver! Escreve \"preferências\" para tentares outra vez mais tarde.",
                ct);
            conversation.SetFlowStep(ConversationFlowStep.None);
            return;
        }

        var rows = classTypes.Take(10).Select(c => new WhatsAppListRow(c.Id.ToString(), c.Name)).ToList();

        await _whatsAppMessageSender.SendListMessageAsync(
            gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber,
            "Que tipo de aulas te interessam?",
            "Escolher aula",
            [new WhatsAppListSection("Aulas disponíveis", rows)],
            ct);
    }

    private async Task SendDayListAsync(Gym gym, Conversation conversation, CancellationToken ct)
    {
        var rows = new List<WhatsAppListRow>
        {
            new($"day_{(int)DayOfWeek.Monday}", "Segunda-feira"),
            new($"day_{(int)DayOfWeek.Tuesday}", "Terça-feira"),
            new($"day_{(int)DayOfWeek.Wednesday}", "Quarta-feira"),
            new($"day_{(int)DayOfWeek.Thursday}", "Quinta-feira"),
            new($"day_{(int)DayOfWeek.Friday}", "Sexta-feira"),
            new($"day_{(int)DayOfWeek.Saturday}", "Sábado"),
            new($"day_{(int)DayOfWeek.Sunday}", "Domingo"),
        };

        await _whatsAppMessageSender.SendListMessageAsync(
            gym.WhatsAppPhoneNumberId, conversation.ContactPhoneNumber,
            "Em que dia da semana queres receber sugestões?",
            "Escolher dia",
            [new WhatsAppListSection("Dias da semana", rows)],
            ct);
    }

    private async Task<string> BuildConfirmationSummaryAsync(NotificationPreference preference, CancellationToken ct)
    {
        var classTypeNames = new List<string>();
        foreach (var classTypeId in preference.SelectedClassTypeIds)
        {
            var classType = await _classTypeRepository.GetByIdAsync(classTypeId, ct);
            if (classType is not null) classTypeNames.Add(classType.Name);
        }

        var slotDescriptions = preference.Slots
            .Select(s => $"{DescribeDay(s.DayOfWeek)} ({DescribeWindow(s.TimeWindow)})")
            .ToList();

        return "Preferências guardadas! ✅\n\n" +
               $"Aulas: {(classTypeNames.Count > 0 ? string.Join(", ", classTypeNames) : "nenhuma selecionada")}\n" +
               $"Quando: {(slotDescriptions.Count > 0 ? string.Join(", ", slotDescriptions) : "nenhum horário selecionado")}\n\n" +
               "Escreve \"preferências\" a qualquer momento para alterares isto.";
    }

    private async Task<(NotificationPreference Preference, bool IsNew)> GetOrCreatePreferenceAsync(Guid gymId, string contactPhoneNumber, CancellationToken ct)
    {
        var existing = await _preferenceRepository.GetByContactAsync(gymId, contactPhoneNumber, ct);
        return existing is not null ? (existing, false) : (new NotificationPreference(gymId, contactPhoneNumber), true);
    }

    private async Task PersistPreferenceAsync(NotificationPreference preference, bool isNew, CancellationToken ct)
    {
        if (isNew)
            await _preferenceRepository.AddAsync(preference, ct);
        else
            await _preferenceRepository.UpdateAsync(preference, ct);
    }

    private static bool TryParseDayId(string? id, out DayOfWeek dayOfWeek)
    {
        dayOfWeek = default;
        if (id is null || !id.StartsWith("day_", StringComparison.Ordinal)) return false;
        if (!int.TryParse(id.AsSpan(4), out var raw) || raw is < 0 or > 6) return false;

        dayOfWeek = (DayOfWeek)raw;
        return true;
    }

    private static bool TryParseWindowId(string id, out NotificationTimeWindow window)
    {
        switch (id)
        {
            case "window_morning": window = NotificationTimeWindow.Morning; return true;
            case "window_afternoon": window = NotificationTimeWindow.Afternoon; return true;
            case "window_evening": window = NotificationTimeWindow.Evening; return true;
            default: window = default; return false;
        }
    }

    private static string DescribeDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Segunda-feira",
        DayOfWeek.Tuesday => "Terça-feira",
        DayOfWeek.Wednesday => "Quarta-feira",
        DayOfWeek.Thursday => "Quinta-feira",
        DayOfWeek.Friday => "Sexta-feira",
        DayOfWeek.Saturday => "Sábado",
        _ => "Domingo"
    };

    private static string DescribeWindow(NotificationTimeWindow window) => window switch
    {
        NotificationTimeWindow.Morning => "manhã",
        NotificationTimeWindow.Afternoon => "tarde",
        _ => "noite"
    };
}
