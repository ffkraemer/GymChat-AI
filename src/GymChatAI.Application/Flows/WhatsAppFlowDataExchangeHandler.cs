using System.Text.Json;
using GymChatAI.Application.Abstractions;

namespace GymChatAI.Application.Flows;

/// <summary>
/// Interprets a decrypted Data Exchange request and builds the (still-plaintext) JSON
/// response. Given our PreferencesFlowJsonBuilder screen is a single terminal screen (its
/// Footer uses the "complete" action, not "navigate"), this only ever needs to handle:
/// - "ping": Meta's periodic health check of the endpoint.
/// - "INIT": the first (and only) screen load, where we inject the gym's ClassTypes.
/// The final form submission ("complete") does NOT come through this endpoint - Meta
/// delivers it as a regular webhook message (interactive.type == "nfm_reply"), handled by
/// ProcessIncomingMessageHandler instead.
/// </summary>
public class WhatsAppFlowDataExchangeHandler
{
    private readonly IWhatsAppFlowTokenStore _tokenStore;
    private readonly IClassTypeRepository _classTypeRepository;

    public WhatsAppFlowDataExchangeHandler(IWhatsAppFlowTokenStore tokenStore, IClassTypeRepository classTypeRepository)
    {
        _tokenStore = tokenStore;
        _classTypeRepository = classTypeRepository;
    }

    public async Task<string> HandleAsync(string decryptedRequestJson, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(decryptedRequestJson);
        var root = doc.RootElement;

        var action = root.TryGetProperty("action", out var actionEl) ? actionEl.GetString() : null;

        if (action == "ping")
            return JsonSerializer.Serialize(new { data = new { status = "active" } });

        var flowToken = root.TryGetProperty("flow_token", out var tokenEl) ? tokenEl.GetString() : null;
        var context = flowToken is not null ? _tokenStore.Resolve(flowToken) : null;

        if (context is null)
        {
            // Unknown/expired token - nothing sensible to return; acknowledge and let the
            // Flow session fail gracefully on WhatsApp's side rather than throwing here.
            return JsonSerializer.Serialize(new { data = new { acknowledged = true } });
        }

        if (action == "INIT")
        {
            var classTypes = await _classTypeRepository.GetActiveByGymAsync(context.GymId, cancellationToken);
            var classTypeItems = classTypes.Select(c => new { id = c.Id.ToString(), title = c.Name }).ToList();

            return JsonSerializer.Serialize(new
            {
                screen = "PREFERENCES",
                data = new { class_types = classTypeItems }
            });
        }

        return JsonSerializer.Serialize(new { data = new { acknowledged = true } });
    }
}
