namespace GymChatAI.Domain.Enums;

/// <summary>Delivery status of a single campaign dispatch to one recipient.</summary>
public enum CampaignMessageStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
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

/// <summary>
/// Where a conversation is in the guided WhatsApp menu flow (onboarding, then notification
/// preferences). None means "ordinary conversation, hand messages to the AI assistant".
/// Everything else means "the next inbound message is expected to be a button/list reply
/// for this specific step", handled by OnboardingFlowHandler instead of the AI.
/// </summary>
/// <summary>What causes a FlowDefinition to start automatically.</summary>
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

/// <summary>Lifecycle status of a conversation with a lead or member.</summary>
public enum ConversationStatus
{
    Open = 1,
    WaitingForHuman = 2,
    Closed = 3
}

/// <summary>Comparison a Condition node applies to a stored variable.</summary>
public enum FlowConditionOperator
{
    Equals = 1,
    NotEquals = 2,
    Contains = 3
}

/// <summary>The kind of step a FlowNode represents, and therefore how the engine executes it.</summary>
public enum FlowNodeType
{
    /// <summary>Sends plain text, then immediately continues to the next node.</summary>
    Message = 1,

    /// <summary>Sends up to 3 buttons and pauses, waiting for a tap.</summary>
    ButtonQuestion = 2,

    /// <summary>Sends a scrollable list (up to 10 rows) and pauses, waiting for a selection.</summary>
    ListQuestion = 3,

    /// <summary>Evaluates a stored variable against a value, then continues down the True or False edge.</summary>
    Condition = 4,

    /// <summary>Runs one predefined domain action (e.g. "save this answer as a notification preference"), then continues.</summary>
    SaveAction = 5,

    /// <summary>Terminates the flow session.</summary>
    End = 6
}

/// <summary>Where a ListQuestion node's options come from.</summary>
public enum FlowOptionsSource
{
    /// <summary>Options are typed directly into the node by whoever built the flow.</summary>
    Static = 1,

    /// <summary>One row per active ClassType of the gym.</summary>
    GymClassTypes = 2,

    /// <summary>The 7 days of the week.</summary>
    DaysOfWeek = 3
}

/// <summary>The fixed set of domain side-effects a SaveAction node can trigger.</summary>
public enum FlowSaveActionType
{
    CompleteOnboardingOptedIn = 1,
    CompleteOnboardingOptedOut = 2,
    SaveClassTypeSelection = 3,
    SaveTimeSlotSelection = 4,
    ResetPreferenceSelections = 5
}

public enum FlowTriggerType
{
    /// <summary>Starts on the very first message from a brand new contact.</summary>
    NewConversation = 1,

    /// <summary>Starts when the inbound text matches TriggerKeyword (case-insensitive).</summary>
    Keyword = 2
}

/// <summary>Supported languages for the AI assistant (POC scope: PT/EN, extensible).</summary>
public enum Language
{
    Unknown = 0,
    Portuguese = 1,
    English = 2,
    Spanish = 3
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

/// <summary>Status of a gym member.</summary>
public enum MemberStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3,
    Cancelled = 4
}

/// <summary>Direction of a WhatsApp message relative to the platform.</summary>
public enum MessageDirection
{
    Inbound = 1,
    Outbound = 2
}

/// <summary>How a message was authored.</summary>
public enum MessageOrigin
{
    Human = 1,
    AiAssistant = 2,
    System = 3
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

/// <summary>Broad time-of-day window a member prefers for class suggestions.</summary>
public enum NotificationTimeWindow
{
    Morning = 1,
    Afternoon = 2,
    Evening = 3
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

/// <summary>Meta's required category classification for a message template.</summary>
public enum WhatsAppTemplateCategory
{
    Marketing = 1,
    Utility = 2,
    Authentication = 3
}

/// <summary>Lifecycle of a message template, mirroring Meta's own review states.</summary>
public enum WhatsAppTemplateStatus
{
    /// <summary>Created locally, not yet submitted to Meta for review.</summary>
    Draft = 1,

    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    Paused = 5,
    Disabled = 6
}

/// <summary>Lifecycle of one contact's run through a FlowDefinition.</summary>
public enum FlowSessionStatus
{
    Active = 1,
    Completed = 2,

    /// <summary>Abandoned deliberately (e.g. a new flow was started over this one).</summary>
    Abandoned = 3
}