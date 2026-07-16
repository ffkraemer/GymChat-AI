using GymChatAI.Infrastructure.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace GymChatAI.Api.Endpoints;

public record CredentialHealthResponse(string Provider, string Status, string Message, string? RenewAt);

/// <summary>
/// Lightweight, read-only checks against each external provider - useful to run before a
/// demo, instead of finding out a token expired only when a real message fails to send.
/// None of these consume real messaging/AI quota (WhatsApp: reads the phone number's own
/// info; AI providers: list models, which is free/near-free on all three).
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapCredentialHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/health").WithTags("Health");

        group.MapGet("/whatsapp", async (IHttpClientFactory httpClientFactory, IOptions<WhatsAppOptions> options, CancellationToken ct) =>
        {
            var opts = options.Value;
            var client = httpClientFactory.CreateClient();

            const string renewAt = "https://business.facebook.com/settings/system-users (System User → Generate Token, Never expire) " +
                                    "ou https://developers.facebook.com/apps → WhatsApp → API Setup (token temporário, ~24h)";

            if (string.IsNullOrWhiteSpace(opts.AccessToken) || string.IsNullOrWhiteSpace(opts.DemoPhoneNumberId))
                return Results.Ok(new CredentialHealthResponse("WhatsApp", "not-configured", "AccessToken ou DemoPhoneNumberId em falta.", renewAt));

            try
            {
                var url = $"{opts.GraphApiBaseUrl.TrimEnd('/')}/{opts.DemoPhoneNumberId}?fields=id";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", opts.AccessToken);

                var response = await client.SendAsync(request, ct);

                if (response.IsSuccessStatusCode)
                    return Results.Ok(new CredentialHealthResponse("WhatsApp", "ok", "Token válido.", null));

                if ((int)response.StatusCode == 401)
                    return Results.Ok(new CredentialHealthResponse("WhatsApp", "expired", "Token expirado ou inválido (401/erro 190).", renewAt));

                var body = await response.Content.ReadAsStringAsync(ct);
                return Results.Ok(new CredentialHealthResponse("WhatsApp", "error", $"Resposta inesperada ({(int)response.StatusCode}): {body}", renewAt));
            }
            catch (Exception ex)
            {
                return Results.Ok(new CredentialHealthResponse("WhatsApp", "error", $"Falha de rede: {ex.Message}", renewAt));
            }
        });

        group.MapGet("/ai", async (
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IOptions<GeminiOptions> geminiOptions,
            IOptions<OpenAIOptions> openAiOptions,
            IOptions<AzureOpenAIOptions> azureOptions,
            CancellationToken ct) =>
        {
            var provider = configuration["AiProvider"]?.Trim().ToLowerInvariant();

            provider ??= !string.IsNullOrWhiteSpace(geminiOptions.Value.ApiKey) ? "gemini"
                : !string.IsNullOrWhiteSpace(openAiOptions.Value.ApiKey) ? "openai"
                : "azureopenai";

            var client = httpClientFactory.CreateClient();

            return provider switch
            {
                "gemini" => Results.Ok(await CheckGeminiAsync(client, geminiOptions.Value, ct)),
                "openai" => Results.Ok(await CheckOpenAiAsync(client, openAiOptions.Value, ct)),
                _ => Results.Ok(await CheckAzureOpenAiAsync(client, azureOptions.Value, ct)),
            };
        });

        return app;
    }

    private static async Task<CredentialHealthResponse> CheckAzureOpenAiAsync(HttpClient client, AzureOpenAIOptions options, CancellationToken ct)
    {
        const string renewAt = "https://portal.azure.com → o teu recurso Azure OpenAI → Keys and Endpoint";

        if (string.IsNullOrWhiteSpace(options.ApiKey) || string.IsNullOrWhiteSpace(options.Endpoint))
            return new CredentialHealthResponse("AzureOpenAI", "not-configured", "ApiKey ou Endpoint em falta.", renewAt);

        try
        {
            var url = $"{options.Endpoint.TrimEnd('/')}/openai/deployments?api-version={options.ApiVersion}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("api-key", options.ApiKey);

            var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return new CredentialHealthResponse("AzureOpenAI", "ok", "Chave válida.", null);

            var body = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode is 401 or 403 ? "expired" : "error";
            return new CredentialHealthResponse("AzureOpenAI", status, $"Resposta {(int)response.StatusCode}: {body}", renewAt);
        }
        catch (Exception ex)
        {
            return new CredentialHealthResponse("AzureOpenAI", "error", $"Falha de rede: {ex.Message}", renewAt);
        }
    }

    private static async Task<CredentialHealthResponse> CheckGeminiAsync(HttpClient client, GeminiOptions options, CancellationToken ct)
    {
        const string renewAt = "https://aistudio.google.com/apikey";

        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return new CredentialHealthResponse("Gemini", "not-configured", "ApiKey em falta.", renewAt);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://generativelanguage.googleapis.com/v1beta/models");
            request.Headers.TryAddWithoutValidation("x-goog-api-key", options.ApiKey);

            var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return new CredentialHealthResponse("Gemini", "ok", "Chave válida.", null);

            var body = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode is 401 or 403 ? "expired" : "error";
            return new CredentialHealthResponse("Gemini", status, $"Resposta {(int)response.StatusCode}: {body}", renewAt);
        }
        catch (Exception ex)
        {
            return new CredentialHealthResponse("Gemini", "error", $"Falha de rede: {ex.Message}", renewAt);
        }
    }

    private static async Task<CredentialHealthResponse> CheckOpenAiAsync(HttpClient client, OpenAIOptions options, CancellationToken ct)
    {
        const string renewAt = "https://platform.openai.com/api-keys";

        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return new CredentialHealthResponse("OpenAI", "not-configured", "ApiKey em falta.", renewAt);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.openai.com/v1/models");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);

            var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return new CredentialHealthResponse("OpenAI", "ok", "Chave válida.", null);

            var body = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode is 401 or 403 ? "expired" : "error";
            return new CredentialHealthResponse("OpenAI", status, $"Resposta {(int)response.StatusCode}: {body}", renewAt);
        }
        catch (Exception ex)
        {
            return new CredentialHealthResponse("OpenAI", "error", $"Falha de rede: {ex.Message}", renewAt);
        }
    }
}