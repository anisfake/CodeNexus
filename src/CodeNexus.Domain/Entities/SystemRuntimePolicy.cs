namespace CodeNexus.Domain.Entities;

public class SystemRuntimePolicy
{
    public Guid SystemRuntimePolicyId { get; set; }
    public string PolicyKey { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string ConfigJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
