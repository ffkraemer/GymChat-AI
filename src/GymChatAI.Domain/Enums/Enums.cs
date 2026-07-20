namespace GymChatAI.Domain.Enums;

/// <summary>Direction of a WhatsApp message relative to the platform.</summary>
public enum MessageDirection
{
    Inbound = 1,
    Outbound = 2
}

/// <summary>Delivery/processing status of a message.</summary>
public enum MessageStatus
{
    Received = 1,
    Processing = 2,
    Sent = 3,
    Delivered = 4,
    Read = 5,
    Failed = 6
}

/// <summary>How a message was authored.</summary>
public enum MessageOrigin
{
    Human = 1,
    AiAssistant = 2,
    System = 3
}

/// <summary>Lifecycle status of a conversation with a lead or member.</summary>
public enum ConversationStatus
{
    Open = 1,
    WaitingForHuman = 2,
    Closed = 3
}

/// <summary>Supported languages for the AI assistant (POC scope: PT/EN, extensible).</summary>
public enum Language
{
    Unknown = 0,
    Portuguese = 1,
    English = 2,
    Spanish = 3
}

/// <summary>Status of a gym member.</summary>
public enum MemberStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
    Cancelled = 4
}

/// <summary>Status of a lead in the qualification funnel.</summary>
public enum LeadStatus
{
    New = 1,
    Contacted = 2,
    Qualified = 3,
    Converted = 4,
    Lost = 5
}

/// <summary>Kind of loyalty campaign, each with its own triggering rule.</summary>
public enum CampaignType
{
    /// <summary>Sent automatically N days after a member joins.</summary>
    Welcome = 1,

    /// <summary>Sent automatically on the member's birthday.</summary>
    Birthday = 2,

    /// <summary>Sent automatically once a member has been inactive for N days.</summary>
    Reactivation = 3,

    /// <summary>Triggered manually by an operator from the Administration Portal.</summary>
    Manual = 4
}

/// <summary>Delivery status of a single campaign dispatch to one recipient.</summary>
public enum CampaignMessageStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}

/// <summary>Status of a queued reply awaiting a retry after the AI provider was unavailable.</summary>
public enum PendingAIReplyStatus
{
    /// <summary>Still waiting to be retried.</summary>
    Pending = 1,

    /// <summary>A retry succeeded - the customer got their answer after all.</summary>
    Resolved = 2,

    /// <summary>Gave up after the maximum number of attempts; the conversation stays escalated to a human.</summary>
    Abandoned = 3
}

/// <summary>Broad time-of-day window a member prefers for class suggestions.</summary>
public enum NotificationTimeWindow
{
    Morning = 1,
    Afternoon = 2,
    Evening = 3
}

/// <summary>
/// Where a conversation is in the guided WhatsApp menu flow (onboarding, then notification
/// preferences). None means "ordinary conversation, hand messages to the AI assistant".
/// Everything else means "the next inbound message is expected to be a button/list reply
/// for this specific step", handled by OnboardingFlowHandler instead of the AI.
/// </summary>
public enum ConversationFlowStep
{
    None = 0,
    AwaitingOnboardingConsent = 1,
    AwaitingClassTypeSelection = 2,
    AwaitingMoreClassTypesDecision = 3,
    AwaitingDaySelection = 4,
    AwaitingTimeWindowSelection = 5,
    AwaitingMoreSlotsDecision = 6
}
