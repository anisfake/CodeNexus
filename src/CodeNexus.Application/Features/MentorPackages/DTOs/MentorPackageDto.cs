namespace CodeNexus.Application.Features.MentorPackages.DTOs;

public record MentorPackageDto(
    Guid MentorPackageId,
    string Name,
    string? Description,
    decimal PriceVnd,
    int SharesFromMentorLimit,
    int ValidationRequestLimit,
    int TaskReviewLimit,
    bool IsActive,
    int DisplayOrder,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
