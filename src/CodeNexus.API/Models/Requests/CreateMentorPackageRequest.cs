namespace CodeNexus.API.Models.Requests;

public record CreateMentorPackageRequest(
    string Name,
    string? Description,
    decimal PriceVnd,
    int SharesFromMentorLimit,
    int ValidationRequestLimit,
    int TaskReviewLimit,
    bool IsActive,
    int DisplayOrder
);
