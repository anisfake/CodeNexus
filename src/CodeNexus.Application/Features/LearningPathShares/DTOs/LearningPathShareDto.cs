using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPathShares.DTOs;

public record LearningPathShareDto(
    Guid ShareId,
    Guid PathId,
    Guid MentorId,
    Guid StudentId,
    LearningPathShareStatus Status,
    DateTime SentAt,
    DateTime? RespondedAt,
    Guid? AcceptedPathId = null,
    decimal? SourceVersionAtAccept = null,
    decimal? IgnoredSourceVersion = null,
    decimal? LastNotifiedSourceVersion = null,
    bool IsTrackingEnabled = true,
    string? InvalidatedReason = null
);
