using System;

namespace GymChatAI.Domain.Entities;

public class Member
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}