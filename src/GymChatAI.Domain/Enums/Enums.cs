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
