using GymChatAI.Domain.Common;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// Records a delivery failure Meta reported to us after the fact, via the WhatsApp status
/// webhook (status="failed") - e.g. the recipient isn't on WhatsApp, or has blocked the
/// business. Distinct from WhatsAppApiError (which is us failing to even call the API):
/// this is Meta itself confirming that an accepted message could not be delivered.
/// </summary>
public class WhatsAppDeliveryFailure : Entity
{
    public Guid GymId { get; private set; }

    public string WhatsAppMessageId { get; private set; } = default!;

    public string RecipientPhoneNumber { get; private set; } = default!;

    public string? ErrorCode { get; private set; }

    public string ErrorMessage { get; private set; } = default!;

    private WhatsAppDeliveryFailure() { }

    public WhatsAppDeliveryFailure(Guid gymId, string whatsAppMessageId, string recipientPhoneNumber, string? errorCode, string errorMessage)
    {
        GymId = gymId;
        WhatsAppMessageId = whatsAppMessageId;
        RecipientPhoneNumber = recipientPhoneNumber;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }
}
