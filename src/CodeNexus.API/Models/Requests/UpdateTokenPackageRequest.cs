namespace CodeNexus.API.Models.Requests;

public record UpdateTokenPackageRequest(
    string Name,
    string? Description,
    decimal PriceVnd,
    decimal CreditedTokens,
    bool IsActive,
    int DisplayOrder
);


