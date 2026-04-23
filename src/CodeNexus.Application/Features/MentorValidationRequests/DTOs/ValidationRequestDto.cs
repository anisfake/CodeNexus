using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.MentorValidationRequests.DTOs;

public record ValidationRequestDto(
    Guid ValidationRequestId,
    Guid PathId,
    string PathTitle,
    Guid StudentId,
    string StudentUsername,
    Guid MentorId,
    string MentorUsername,
    string? StudentNote,
    string? MentorFeedback,
    ValidationRequestStatus Status,
    DateTime CreatedAt,
    DateTime? RespondedAt
);
