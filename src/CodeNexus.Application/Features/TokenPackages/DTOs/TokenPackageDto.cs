namespace CodeNexus.Application.Features.TokenPackages.DTOs;

public record TokenPackageDto(
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

