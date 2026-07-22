using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymChatAI.Application.Abstractions;
using GymChatAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymChatAI.Infrastructure.WhatsApp;

/// <summary>
/// Lets the Administration Portal create and track WhatsApp message templates without the
/// gym ever needing to log into Meta Business Manager directly.
/// </summary>
public class WhatsAppTemplateManagementClient : IWhatsAppTemplateManagementClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppTemplateManagementClient> _logger;

    public WhatsAppTemplateManagementClient(HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<WhatsAppTemplateManagementClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(options.Value.GraphApiBaseUrl.TrimEnd('/') + "/");

        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.AccessToken);
    }

    public async Task<WhatsAppTemplateSubmissionResult> SubmitTemplateAsync(
        string whatsAppBusinessAccountId,
        string name,
        string language,
        string category,
        string bodyText,
        IReadOnlyList<string> variableNames,
        CancellationToken cancellationToken = default)
    {
        // Meta uses positional {{1}}, {{2}}... placeholders - convert our friendly
        // {VariableName} syntax, in the order the variables first appear.
        var metaBodyText = bodyText;
        for (var i = 0; i < variableNames.Count; i++)
            metaBodyText = metaBodyText.Replace($"{{{variableNames[i]}}}", $"{{{{{i + 1}}}}}");

        TemplateComponentExample? example = null;
        if (variableNames.Count > 0)
        {
            var sampleValues = variableNames.Select(BuildSampleValue).ToList();
            example = new TemplateComponentExample([sampleValues]);
        }

        var payload = new CreateTemplateRequest(
            name, language, category,
            [new TemplateComponent("BODY", metaBodyText, example)]);

        var response = await _httpClient.PostAsJsonAsync($"{whatsAppBusinessAccountId}/message_templates", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to submit WhatsApp template '{TemplateName}': {StatusCode} {Body}", name, response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<CreateTemplateResponse>(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(result?.Id))
            throw new InvalidOperationException("WhatsApp API did not return a template id.");

        return new WhatsAppTemplateSubmissionResult(result.Id);
    }

    public async Task<IReadOnlyList<WhatsAppTemplateRemoteStatus>> GetTemplateStatusesAsync(
        string whatsAppBusinessAccountId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            $"{whatsAppBusinessAccountId}/message_templates?fields=id,name,status,rejected_reason", cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Failed to fetch template statuses for WABA {WabaId}: {StatusCode} {Body}", whatsAppBusinessAccountId, response.StatusCode, body);
            return [];
        }

        var result = await response.Content.ReadFromJsonAsync<ListTemplatesResponse>(cancellationToken: cancellationToken);

        return (result?.Data ?? [])
            .Where(t => t.Id is not null && t.Status is not null)
            .Select(t => new WhatsAppTemplateRemoteStatus(t.Id!, t.Status!, t.RejectedReason))
            .ToList();
    }

    /// <summary>
    /// Meta requires a plausible example value for every variable when submitting a template
    /// with placeholders. We don't have real customer data at draft time, so generate a
    /// reasonable stand-in - recognizing a couple of common variable names for a better example.
    /// </summary>
    private static string BuildSampleValue(string variableName) => variableName switch
    {
        "FirstName" or "FullName" => "João",
        "GymName" => "GymChat Demo Fitness Club",
        _ => "Exemplo"
    };
}
