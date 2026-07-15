namespace GymChatAI.Infrastructure.Options;

/// <summary>Configuration bound from appsettings/environment for Azure OpenAI.</summary>
public class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    /// <summary>Resource endpoint, e.g. https://your-resource.openai.azure.com</summary>
    public string Endpoint { get; set; } = default!;

    public string ApiKey { get; set; } = default!;

    /// <summary>Deployment name of the chat model (e.g. a GPT-5 deployment).</summary>
    public string DeploymentName { get; set; } = default!;

    public string ApiVersion { get; set; } = "2024-10-21";

    public double Temperature { get; set; } = 0.4;

    public int MaxOutputTokens { get; set; } = 500;
}
