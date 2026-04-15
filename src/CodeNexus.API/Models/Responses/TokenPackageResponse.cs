namespace CodeNexus.API.Models.Responses;

public record TokenPackageResponse(
    Guid TokenPackageId,
    string Name,
    string? Description,
    decimal PriceVnd,
    decimal CreditedBalanceVnd,
    bool IsActive,
    int DisplayOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

