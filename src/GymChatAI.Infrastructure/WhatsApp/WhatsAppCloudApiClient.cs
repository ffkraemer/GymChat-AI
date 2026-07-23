using System.Collections.Concurrent;
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
///
/// Also enforces a duplicate-message guard (protects the number's quality rating from
/// accidental repeat sends - see the Compliance Dashboard) and records every failed call to
/// IWhatsAppApiErrorRepository, feeding that same dashboard's error history.
/// </summary>
public class WhatsAppCloudApiClient : IWhatsAppMessageSender
{
    // Identical text sent to the same recipient within this window is treated as an
    // accidental repeat and skipped, rather than actually sent again.
    private static readonly TimeSpan DuplicateTextWindow = TimeSpan.FromMinutes(5);

    // Static and keyed by sender+recipient: typed HttpClients are re-created per request by
    // HttpClientFactory, so this has to live outside the instance to actually catch repeats
    // across separate incoming webhook calls.
    private static readonly ConcurrentDictionary<string, (string Text, DateTimeOffset SentAtUtc)> RecentTextSends = new();

    private readonly HttpClient _httpClient;
    private readonly WhatsAppOptions _options;
    private readonly IGymRepository _gymRepository;
    private readonly IWhatsAppApiErrorRepository _errorRepository;
    private readonly ILogger<WhatsAppCloudApiClient> _logger;

    public WhatsAppCloudApiClient(
        HttpClient httpClient,
        IOptions<WhatsAppOptions> options,
        IGymRepository gymRepository,
        IWhatsAppApiErrorRepository errorRepository,
        ILogger<WhatsAppCloudApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _gymRepository = gymRepository;
        _errorRepository = errorRepository;
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
        var dedupeKey = $"{fromPhoneNumberId}:{toPhoneNumber}";
        if (RecentTextSends.TryGetValue(dedupeKey, out var last)
            && last.Text == text
            && DateTimeOffset.UtcNow - last.SentAtUtc < DuplicateTextWindow)
        {
            _logger.LogWarning(
                "Skipped duplicate text message to {ToPhoneNumber} - identical content was already sent within the last {Minutes} minute(s). " +
                "This guard protects the number's WhatsApp quality rating from accidental repeat sends.",
                toPhoneNumber, DuplicateTextWindow.TotalMinutes);
            throw new DuplicateMessageException(toPhoneNumber);
        }

        var payload = SendTextMessageRequest.Create(toPhoneNumber, text);
        var wamid = await PostAndExtractMessageIdAsync(fromPhoneNumberId, payload, toPhoneNumber, cancellationToken);

        RecentTextSends[dedupeKey] = (text, DateTimeOffset.UtcNow);
        return wamid;
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

    public async Task<string> SendFlowMessageAsync(
        string fromPhoneNumberId,
        string toPhoneNumber,
        string bodyText,
        string flowCtaButtonText,
        string metaFlowId,
        string flowToken,
        string screenId,
        CancellationToken cancellationToken = default)
    {
        var payload = SendFlowMessageRequest.Create(toPhoneNumber, bodyText, flowCtaButtonText, metaFlowId, flowToken, screenId);
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

            await RecordErrorAsync(fromPhoneNumberId, (int)response.StatusCode, errorBody, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<SendMessageResponse>(cancellationToken: cancellationToken);
        var wamid = result?.Messages?.FirstOrDefault()?.Id;

        if (string.IsNullOrWhiteSpace(wamid))
            throw new InvalidOperationException("WhatsApp API did not return a message id.");

        return wamid;
    }

    /// <summary>
    /// Persists a WhatsAppApiError for the Compliance Dashboard. Best-effort: a failure here
    /// (e.g. the gym lookup or the write itself failing) must never mask or replace the
    /// original send failure, so any exception is swallowed after logging.
    /// </summary>
    private async Task RecordErrorAsync(string fromPhoneNumberId, int statusCode, string errorBody, CancellationToken cancellationToken)
    {
        try
        {
            var gym = await _gymRepository.GetByWhatsAppPhoneNumberIdAsync(fromPhoneNumberId, cancellationToken);
            if (gym is null) return;

            string? errorCode = null;
            var errorMessage = errorBody;

            try
            {
                var parsed = System.Text.Json.JsonSerializer.Deserialize<WhatsAppApiErrorResponse>(errorBody);
                if (parsed?.Error is not null)
                {
                    errorCode = parsed.Error.Code?.ToString();
                    errorMessage = parsed.Error.Message ?? errorBody;
                }
            }
            catch
            {
                // Not JSON, or an unexpected shape - just keep the raw body as the message.
            }

            var error = new Domain.Entities.WhatsAppApiError(gym.Id, "messages", statusCode, errorCode, errorMessage);
            await _errorRepository.AddAsync(error, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record WhatsAppApiError for {PhoneNumberId}.", fromPhoneNumberId);
        }
    }
}
