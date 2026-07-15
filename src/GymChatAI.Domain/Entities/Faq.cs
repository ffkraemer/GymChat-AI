using GymChatAI.Domain.Common;

namespace GymChatAI.Domain.Entities;

/// <summary>
/// Knowledge base entry used to ground the AI assistant's answers
/// (retrieval-augmented prompting for the POC; can evolve into a vector
/// index in later phases).
/// </summary>
public class Faq : Entity
{
    public Faq(Guid gymId, string question, string answer, string? category = null)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("Question is required.", nameof(question));
        if (string.IsNullOrWhiteSpace(answer))
            throw new ArgumentException("Answer is required.", nameof(answer));

        GymId = gymId;
        Question = question;
        Answer = answer;
        Category = category;
    }

    private Faq()
    { }

    public string Answer { get; private set; } = default!;

    public string? Category { get; private set; }

    public Guid GymId { get; private set; }

    public bool IsActive { get; private set; } = true;

    public string Question { get; private set; } = default!;

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void Update(string question, string answer, string? category)
    {
        if (!string.IsNullOrWhiteSpace(question)) Question = question;
        if (!string.IsNullOrWhiteSpace(answer)) Answer = answer;
        Category = category;
        Touch();
    }
}