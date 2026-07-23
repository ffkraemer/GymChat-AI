using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using GymChatAI.Application.Abstractions;
using GymChatAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymChatAI.Infrastructure.WhatsApp.Flows;

/// <summary>
/// Manages a WhatsApp Flow's lifecycle via the Graph API - create, upload/replace its JSON
/// definition, publish, and check status - plus the one-time per-WABA encryption key
/// registration. Lets all of this happen from our Administration Portal instead of Meta's
/// WhatsApp Manager UI.
/// </summary>
public class WhatsAppFlowManagementClient : IWhatsAppFlowManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppFlowManagementClient> _logger;

    public WhatsAppFlowManagementClient(HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<WhatsAppFlowManagementClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(options.Value.GraphApiBaseUrl.TrimEnd('/') + "/");

        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.AccessToken);
    }

    public async Task<CreateFlowResult> CreateFlowAsync(
        string whatsAppBusinessAccountId, string name, IReadOnlyList<string> categories, CancellationToken cancellationToken = default)
    {
        var payload = new CreateFlowRequest(name, categories.ToList());
        var response = await _httpClient.PostAsJsonAsync($"{whatsAppBusinessAccountId}/flows", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to create WhatsApp Flow '{Name}': {StatusCode} {Body}", name, response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<CreateFlowResponse>(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(result?.Id))
            throw new InvalidOperationException("WhatsApp API did not return a Flow id.");

        var validationErrors = (result.ValidationErrors ?? [])
            .Select(e => new WhatsAppFlowValidationError(e.Error, e.Message))
            .ToList();

        return new CreateFlowResult(result.Id, validationErrors);
    }

    public async Task<string?> GetFlowStatusAsync(string metaFlowId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"{metaFlowId}?fields=status", cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<FlowStatusResponse>(cancellationToken: cancellationToken);
        return result?.Status;
    }

    public async Task<bool> PublishFlowAsync(string metaFlowId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"{metaFlowId}/publish", content: null, cancellationToken);
        if (response.IsSuccessStatusCode) return true;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("Failed to publish Flow {MetaFlowId}: {StatusCode} {Body}", metaFlowId, response.StatusCode, body);
        return false;
    }

    public async Task<bool> RegisterEncryptionKeyAsync(string phoneNumberId, string publicKeyPem, CancellationToken cancellationToken = default)
    {
        // Meta expects this as a urlencoded form field, not JSON, per the documented endpoint shape.
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["business_public_key"] = publicKeyPem });

        var response = await _httpClient.PostAsync($"{phoneNumberId}/whatsapp_business_encryption", form, cancellationToken);
        if (response.IsSuccessStatusCode) return true;

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger.LogWarning("Failed to register encryption key for phone number {PhoneNumberId}: {StatusCode} {Body}", phoneNumberId, response.StatusCode, body);
        return false;
    }

    public async Task<UpdateFlowJsonResult> UpdateFlowJsonAsync(string metaFlowId, string flowJson, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StringContent(flowJson, Encoding.UTF8, "application/json");
        form.Add(fileContent, "file", "flow.json");
        form.Add(new StringContent("flow.json"), "name");
        form.Add(new StringContent("FLOW_JSON"), "asset_type");

        var response = await _httpClient.PostAsync($"{metaFlowId}/assets", form, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to upload Flow JSON for {MetaFlowId}: {StatusCode} {Body}", metaFlowId, response.StatusCode, body);
            return new UpdateFlowJsonResult(false, []);
        }

        // Meta can return 200 with success:true but still list validation_errors - the flow
        // stays in DRAFT if there are any, so callers should surface these to the admin.
        var result = System.Text.Json.JsonSerializer.Deserialize<UpdateFlowJsonResponse>(body);
        var validationErrors = (result?.ValidationErrors ?? [])
            .Select(e => new WhatsAppFlowValidationError(e.Error, e.Message))
            .ToList();

        return new UpdateFlowJsonResult(result?.Success ?? false, validationErrors);
    }
}