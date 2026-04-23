using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities;

public class LearningPathValidationRequest
{
    public Guid ValidationRequestId { get; set; }
    public Guid PathId { get; set; }
    public Guid StudentId { get; set; }
    public Guid MentorId { get; set; }
    public string? StudentNote { get; set; }
    public string? MentorFeedback { get; set; }
    public ValidationRequestStatus Status { get; set; } = ValidationRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual LearningPath LearningPath { get; set; } = null!;
    public virtual User Student { get; set; } = null!;
    public virtual User Mentor { get; set; } = null!;
}
