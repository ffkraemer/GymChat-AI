namespace GymChatAI.Infrastructure.Options;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string ApiKey { get; set; } = default!;

    public string Model { get; set; } = "gemini-flash-latest";

    public double Temperature { get; set; } = 0.4;

    public int MaxOutputTokens { get; set; } = 500;
}