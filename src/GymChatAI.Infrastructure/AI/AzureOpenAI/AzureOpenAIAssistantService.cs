using System.Net.Http.Headers;
using System.Net.Http.Json;
using GymChatAI.Application.Abstractions;
using GymChatAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GymChatAI.Infrastructure.AI;

/// <summary>
/// Calls the Azure OpenAI Chat Completions REST API directly (no Azure SDK dependency),
/// keeping the footprint small and the integration transparent for the POC.
/// </summary>
public class AzureOpenAIAssistantService : IAIAssistantService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureOpenAIAssistantService> _logger;
    private readonly AzureOpenAIOptions _options;

    public AzureOpenAIAssistantService(HttpClient httpClient, IOptions<AzureOpenAIOptions> options, ILogger<AzureOpenAIAssistantService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(_options.Endpoint.TrimEnd('/') + "/");

        if (!_httpClient.DefaultRequestHeaders.Contains("api-key"))
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("api-key", _options.ApiKey);

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string> GenerateReplyAsync(AIAssistantContext context, string userMessage, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage> { new("system", SystemPromptBuilder.Build(context)) };

        foreach (var turn in context.History)
            messages.Add(new ChatMessage(turn.Role == "user" ? "user" : "assistant", turn.Content));

        messages.Add(new ChatMessage("user", userMessage));

        var request = new ChatCompletionRequest(messages, _options.Temperature, _options.MaxOutputTokens);

        var url = $"openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Azure OpenAI returned {StatusCode}: {Body}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        var reply = result?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(reply))
            throw new InvalidOperationException("Azure OpenAI did not return a reply.");

        return reply.Trim();
    }
}