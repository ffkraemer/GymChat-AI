using System.Collections.Concurrent;
using GymChatAI.Application.Abstractions;

namespace GymChatAI.Infrastructure.WhatsApp.Flows;

/// <summary>In-memory implementation of IWhatsAppFlowTokenStore - static so it survives across the typed HttpClient instances created per request.</summary>
public class InMemoryWhatsAppFlowTokenStore : IWhatsAppFlowTokenStore
{
    private static readonly ConcurrentDictionary<string, WhatsAppFlowTokenContext> Tokens = new();

    public string CreateToken(Guid gymId, string contactPhoneNumber)
    {
        var token = Guid.NewGuid().ToString("N");
        Tokens[token] = new WhatsAppFlowTokenContext(gymId, contactPhoneNumber);
        return token;
    }

    public WhatsAppFlowTokenContext? Resolve(string token) => Tokens.GetValueOrDefault(token);
}
