using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymChatAI.Application.Abstractions;
using GymChatAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymChatAI.Infrastructure.WhatsApp;

/// <summary>
/// Reads a phone number's live compliance data from the Graph API. Separate HttpClient
/// registration from WhatsAppCloudApiClient (both point at the same base URL/token, but
/// this one is read-only diagnostics, not message sending) - keeps the two concerns from
/// bleeding into each other.
/// </summary>
public class WhatsAppComplianceClient : IWhatsAppComplianceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppComplianceClient> _logger;

    public WhatsAppComplianceClient(HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<WhatsAppComplianceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(options.Value.GraphApiBaseUrl.TrimEnd('/') + "/");

        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.AccessToken);
    }

    public async Task<WhatsAppPhoneNumberHealth> GetPhoneNumberHealthAsync(string phoneNumberId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{phoneNumberId}?fields=quality_rating,whatsapp_business_manager_messaging_limit,name_status", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to fetch phone number health for {PhoneNumberId}: {StatusCode} {Body}", phoneNumberId, response.StatusCode, body);
                return new WhatsAppPhoneNumberHealth("UNKNOWN", null, null);
            }

            var result = await response.Content.ReadFromJsonAsync<WhatsAppPhoneNumberHealthResponse>(cancellationToken: cancellationToken);
            return new WhatsAppPhoneNumberHealth(
                result?.QualityRating ?? "UNKNOWN",
                result?.MessagingLimit,
                result?.NameStatus);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching phone number health for {PhoneNumberId}.", phoneNumberId);
            return new WhatsAppPhoneNumberHealth("UNKNOWN", null, null);
        }
    }
}
