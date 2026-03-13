namespace CodeNexus.Domain.Entities;

public class TokenBlacklist
{
    public Guid Id { get; set; }
    public string TokenId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime BlacklistedAt { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
}
