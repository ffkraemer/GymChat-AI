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

    /// <summary>
    /// Resolves an ordered list of {VariableName} placeholders (as produced by
    /// WhatsAppMessageTemplate.ExtractVariableNames) into their actual values for one
    /// member - this is the shape Meta's template-send API expects (positional {{1}},
    /// {{2}}... parameters), as opposed to Render's single interpolated string.
    /// </summary>
    public static IReadOnlyList<string> ResolveParameterValues(IReadOnlyList<string> variableNames, Member member, string gymName) =>
        variableNames.Select(name => name switch
        {
            "FirstName" => member.FirstName,
            "FullName" => member.FullName,
            "GymName" => gymName,
            _ => ""
        }).ToList();
}
