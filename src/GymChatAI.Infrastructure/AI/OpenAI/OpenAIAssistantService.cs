using GymChatAI.Application.Abstractions;
using GymChatAI.Infrastructure.AI;
using GymChatAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace GymChatAI.Infrastructure.Ai;

public class OpenAIAssistantService : IAIAssistantService
{
    private const string BaseUrl = "https://api.openai.com/v1/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIAssistantService> _logger;
    private readonly OpenAiOptions _options;

    public OpenAIAssistantService(HttpClient httpClient, IOptions<OpenAiOptions> options, ILogger<OpenAIAssistantService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(BaseUrl);

        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
    }

    public async Task<string> GenerateReplyAsync(AIAssistantContext context, string userMessage, CancellationToken cancellationToken = default)
    {
        var messages = new List<ChatMessage> { new("system", SystemPromptBuilder.Build(context)) };

        foreach (var turn in context.History)
            messages.Add(new ChatMessage(turn.Role == "user" ? "user" : "assistant", turn.Content));

        messages.Add(new ChatMessage("user", userMessage));

        var request = new OpenAiChatCompletionRequest(_options.Model, messages, _options.Temperature, _options.MaxOutputTokens);

        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("OpenAI API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
        var reply = result?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(reply))
            throw new InvalidOperationException("OpenAI API did not return a reply.");

        return reply.Trim();
    }
}