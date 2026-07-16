using GymChatAI.Application.Abstractions;
using GymChatAI.Infrastructure.AI;
using GymChatAI.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace GymChatAI.Infrastructure.AI;

public class GeminiAIAssistantService : IAIAssistantService
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiAIAssistantService> _logger;
    private readonly GeminiOptions _options;

    public GeminiAIAssistantService(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiAIAssistantService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        if (_httpClient.BaseAddress is null)
            _httpClient.BaseAddress = new Uri(BaseUrl);

        if (!_httpClient.DefaultRequestHeaders.Contains("x-goog-api-key"))
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey);
    }

    public async Task<string> GenerateReplyAsync(AIAssistantContext context, string userMessage, CancellationToken cancellationToken = default)
    {
        var contents = new List<GeminiContent>();

        foreach (var turn in context.History)
            contents.Add(new GeminiContent(turn.Role == "user" ? "user" : "model", [new GeminiPart(turn.Content)]));

        contents.Add(new GeminiContent("user", [new GeminiPart(userMessage)]));

        var request = new GeminiGenerateContentRequest(
            Contents: contents,
            SystemInstruction: new GeminiSystemInstruction([new GeminiPart(SystemPromptBuilder.Build(context))]),
            GenerationConfig: new GeminiGenerationConfig(_options.Temperature,
                                                         _options.MaxOutputTokens,
                                                         new GeminiThinkingConfig("MINIMAL")));

        var url = $"models/{_options.Model}:generateContent";
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Gemini API returned {StatusCode}: {Body}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var result = await response.Content.ReadFromJsonAsync<GeminiGenerateContentResponse>(cancellationToken: cancellationToken);
        var reply = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(reply))
            throw new InvalidOperationException("Gemini API did not return a reply.");

        return reply.Trim();
    }
}