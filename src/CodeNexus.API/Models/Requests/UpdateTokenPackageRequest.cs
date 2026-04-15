namespace CodeNexus.API.Models.Requests;

public record UpdateTokenPackageRequest(
    string Name,
    string? Description,
    decimal PriceVnd,
    decimal CreditedBalanceVnd,
    bool IsActive,
    int DisplayOrder
);

