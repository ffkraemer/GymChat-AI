using System.Net.Http.Headers;
using GymChatAI.Application.Abstractions;
using GymChatAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymChatAI.Infrastructure.WhatsApp;

/// <summary>
/// Calls the Graph API to link our App to a WABA for webhook delivery - the same call we
/// used to make manually through Graph API Explorer every time a new gym was onboarded.
/// </summary>
public class WhatsAppWabaAdminClient : IWhatsAppWabaAdminClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppWabaAdminClient> _logger;

    public WhatsAppWabaAdminClient(HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<WhatsAppWabaAdminClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(options.Value.GraphApiBaseUrl.TrimEnd('/') + "/");

        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.AccessToken);
    }

    public async Task<bool> SubscribeAppToWabaAsync(string whatsAppBusinessAccountId, CancellationToken cancellationToken = default)
    {
        try
        {
            // No request body needed - the access token's own App identity is what gets subscribed.
            var response = await _httpClient.PostAsync($"{whatsAppBusinessAccountId}/subscribed_apps", content: null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Subscribed our App to WABA {WabaId} for webhook delivery.", whatsAppBusinessAccountId);
                return true;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Failed to subscribe our App to WABA {WabaId}: {StatusCode} {Body}. " +
                "The gym's WABA id was still saved - subscribe manually via Graph API Explorer if this keeps failing.",
                whatsAppBusinessAccountId, response.StatusCode, body);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error subscribing our App to WABA {WabaId}.", whatsAppBusinessAccountId);
            return false;
        }
    }
}
