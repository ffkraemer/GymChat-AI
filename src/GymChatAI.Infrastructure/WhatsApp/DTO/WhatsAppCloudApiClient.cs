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
    private readonly ILogger<WhatsAppCloudApiClient> _logger;
    private readonly WhatsAppOptions _options;

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

    public async Task<string> SendButtonMessageAsync(
        string fromPhoneNumberId,
        string toPhoneNumber,
        string bodyText,
        IReadOnlyList<WhatsAppButtonOption> buttons,
        CancellationToken cancellationToken = default)
    {
        if (buttons.Count is 0 or > 3)
            throw new ArgumentException("WhatsApp button messages support between 1 and 3 buttons.", nameof(buttons));

        var payload = SendButtonMessageRequest.Create(
            toPhoneNumber, bodyText, buttons.Select(b => ButtonPayload.Create(b.Id, b.Title)).ToList());

        return await PostAndExtractMessageIdAsync(fromPhoneNumberId, payload, toPhoneNumber, cancellationToken);
    }

    public async Task<string> SendListMessageAsync(
        string fromPhoneNumberId,
        string toPhoneNumber,
        string bodyText,
        string buttonText,
        IReadOnlyList<WhatsAppListSection> sections,
        CancellationToken cancellationToken = default)
    {
        var totalRows = sections.Sum(s => s.Rows.Count);
        if (totalRows is 0 or > 10)
            throw new ArgumentException("WhatsApp list messages support between 1 and 10 rows in total.", nameof(sections));

        var sectionPayloads = sections
            .Select(s => new ListSectionPayload(
                s.Title,
                s.Rows.Select(r => new ListRowPayload(r.Id, r.Title, r.Description)).ToList()))
            .ToList();

        var payload = SendListMessageRequest.Create(toPhoneNumber, bodyText, buttonText, sectionPayloads);
        return await PostAndExtractMessageIdAsync(fromPhoneNumberId, payload, toPhoneNumber, cancellationToken);
    }

    public async Task<string> SendTextMessageAsync(
                string fromPhoneNumberId,
        string toPhoneNumber,
        string text,
        CancellationToken cancellationToken = default)
    {
        var payload = SendTextMessageRequest.Create(toPhoneNumber, text);
        return await PostAndExtractMessageIdAsync(fromPhoneNumberId, payload, toPhoneNumber, cancellationToken);
    }

    private async Task<string> PostAndExtractMessageIdAsync<TPayload>(
        string fromPhoneNumberId, TPayload payload, string toPhoneNumber, CancellationToken cancellationToken)
    {
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