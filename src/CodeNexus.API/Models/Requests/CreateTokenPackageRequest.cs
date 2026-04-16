namespace CodeNexus.API.Models.Requests;

public record CreateTokenPackageRequest(
    string Name,
    string? Description,
    decimal PriceVnd,
    decimal CreditedTokens,
    bool IsActive,
    int DisplayOrder
);


