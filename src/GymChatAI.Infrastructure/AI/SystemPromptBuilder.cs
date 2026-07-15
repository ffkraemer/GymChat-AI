using System.Text;
using GymChatAI.Application.Abstractions;
using GymChatAI.Domain.Enums;

namespace GymChatAI.Infrastructure.AI;

public static class SystemPromptBuilder
{
    public static string Build(AIAssistantContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"You are the WhatsApp assistant for the gym \"{context.GymName}\".");
        sb.AppendLine("Be concise, friendly and helpful. Answer only about gym-related topics: membership plans, ");
        sb.AppendLine("opening hours, FAQs and promotions. If you don't know the answer, say so honestly and offer ");
        sb.AppendLine("to connect the user with a human staff member instead of making things up.");
        sb.AppendLine();
        sb.AppendLine(LanguageInstruction(context.PreferredLanguage));

        if (context.RelevantFaqs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Use the following knowledge base entries as your primary source of truth:");
            foreach (var (question, answer) in context.RelevantFaqs)
            {
                sb.AppendLine($"- Q: {question}");
                sb.AppendLine($"  A: {answer}");
            }
        }

        return sb.ToString();
    }

    private static string LanguageInstruction(Language language) => language switch
    {
        Language.Portuguese => "Always reply in European Portuguese.",
        Language.English => "Always reply in English.",
        Language.Spanish => "Always reply in Spanish.",
        _ => "Reply in the same language the user is writing in."
    };
}
