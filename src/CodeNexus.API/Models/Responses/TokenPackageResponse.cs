namespace CodeNexus.API.Models.Responses;

public record TokenPackageResponse(
    Guid TokenPackageId,
    string Name,
    string? Description,
    decimal PriceVnd,
    decimal CreditedTokens,
    bool IsActive,
    int DisplayOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);


