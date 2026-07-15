namespace GymChatAI.Infrastructure.Options;

public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = default!;

    public string Model { get; set; } = "gpt-4o-mini";

    public double Temperature { get; set; } = 0.4;

    public int MaxOutputTokens { get; set; } = 500;
}