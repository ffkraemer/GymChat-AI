using GymChatAI.Domain.Entities;

namespace GymChatAI.Application.Loyalty;

/// <summary>Resolves {Placeholder} tokens in a campaign message template.</summary>
public static class MessageTemplateRenderer
{
    public static string Render(string template, Member member, string gymName) =>
        template
            .Replace("{FirstName}", member.FirstName)
            .Replace("{FullName}", member.FullName)
            .Replace("{GymName}", gymName);
}
