namespace CodeNexus.Domain.Entities;

public class TokenPackage
{
    public Guid TokenPackageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal PriceVnd { get; set; }
    public decimal CreditedBalanceVnd { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

