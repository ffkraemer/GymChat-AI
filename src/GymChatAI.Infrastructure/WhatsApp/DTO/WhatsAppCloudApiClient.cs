using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymChatAI.Application.Abstractions;
using GymChatAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymChatAI.Infrastructure.WhatsApp;

/// <summary>
/// Sends outbound messages through the WhatsApp Business Cloud API (Graph API).
/// Uses a plain typed HttpClient - no third-party SDK dependency.
/// </summary>
public class WhatsAppCloudApiClient : IWhatsAppMessageSender
{
    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;
    private readonly ILogger<WhatsAppCloudApiClient> _logger;

    public WhatsAppCloudApiClient(HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<WhatsAppCloudApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(_options.GraphApiBaseUrl.TrimEnd('/') + "/");

        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
    }

    public async Task<string> SendTextMessageAsync(
        string fromPhoneNumberId,
        string toPhoneNumber,
        string text,
        CancellationToken cancellationToken = default)
    {
        var payload = SendTextMessageRequest.Create(toPhoneNumber, text);

        var response = await _httpClient.PostAsJsonAsync($"{fromPhoneNumberId}/messages", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "WhatsApp API returned {StatusCode} sending to {ToPhoneNumber}: {Body}",
                response.StatusCode, toPhoneNumber, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
        var wamid = result?.Messages?.FirstOrDefault()?.Id;

        if (string.IsNullOrWhiteSpace(wamid))
            throw new InvalidOperationException("WhatsApp API did not return a message id.");

        return wamid;
    }
}
