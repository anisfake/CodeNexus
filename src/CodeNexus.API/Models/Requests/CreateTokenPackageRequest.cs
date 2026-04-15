namespace CodeNexus.API.Models.Requests;

public record CreateTokenPackageRequest(
    string Name,
    string? Description,
    decimal PriceVnd,
    decimal CreditedBalanceVnd,
    bool IsActive,
    int DisplayOrder
);

