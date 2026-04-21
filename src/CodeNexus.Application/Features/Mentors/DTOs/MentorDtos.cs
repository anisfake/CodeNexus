namespace CodeNexus.Application.Features.Mentors.DTOs;

public record GetMentorsRequest(
    int PageNumber = 1,
    int PageSize = 12,
    string? SearchTerm = null);

public record MentorListItemDto(
    Guid MentorId,
    string Username,
    string? FirstName,
    string? LastName,
    string? FullName,
    string? AvatarUrl,
    string? Bio,
    double AverageRating,
    int TotalReviews,
    List<string> Specializations);

public record MentorReviewDto(
    Guid RatingId,
    Guid StudentId,
    string StudentName,
    string? StudentAvatarUrl,
    int Score,
    string? Comment,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record MentorProfileDto(
    Guid MentorId,
    string Username,
    string? FirstName,
    string? LastName,
    string? FullName,
    string? AvatarUrl,
    string? Bio,
    double AverageRating,
    int TotalReviews,
    List<string> Specializations,
    List<string> SpecializedSubjects,
    MentorReviewDto? MyReview,
    List<MentorReviewDto> RecentReviews);

public record UpsertMentorReviewRequest(
    int Score,
    string? Comment);

public record UpsertMentorReviewResponseDto(
    Guid MentorId,
    Guid StudentId,
    int Score,
    string? Comment,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    double AverageRating,
    int TotalReviews);
