namespace CodeNexus.Application.Features.TokenPackages.DTOs;

public record TokenPackageDto(
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
