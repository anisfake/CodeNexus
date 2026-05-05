namespace CodeNexus.Domain.Entities;

public class MentorRating
{
    public Guid RatingId { get; set; }
    public Guid MentorId { get; set; }
    public virtual User Mentor { get; set; } = null!;
    public Guid StudentId { get; set; }
    public virtual User Student { get; set; } = null!;
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
