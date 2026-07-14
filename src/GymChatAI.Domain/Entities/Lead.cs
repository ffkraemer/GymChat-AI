using System;

namespace GymChatAI.Domain.Entities;

public class Lead
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}