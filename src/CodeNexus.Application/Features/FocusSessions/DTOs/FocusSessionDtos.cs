using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.FocusSessions.DTOs;

public record FocusSessionDto(
    Guid SessionId,
    Guid TaskId,
    string Title,
    DateTime StartTime,
    DateTime? EndTime,
    int PlannedDurationMinutes,
    int? ActualDurationMinutes,
    string SessionStatus,
    string SessionType,
    string? SubmittedCode,
    string? SubmittedSummary,
    string? AIFeedback,
    int? VerificationScore,
    bool IsVerified,
    DateTime CreatedAt
);

public record StartSessionRequest(
    Guid TaskId,
    SessionType SessionType = SessionType.Pomodoro,
    int? PlannedDurationMinutes = null,
    string? Title = null
);

public record CompleteSessionRequest(
    string? SubmittedCode = null,
    string? SubmittedSummary = null,
    string? SubmittedQuizAnswers = null,
    bool IsEarlyCompletion = false,
    SubmissionType SubmissionType = SubmissionType.Progress
);

public record CompleteSessionWithRawCodeRequest(
    string? RawCode = null,
    string? RawSummary = null,
    bool IsEarlyCompletion = false,
    SubmissionType SubmissionType = SubmissionType.Progress
);

public record StartSessionResponseDto(
    Guid SessionId,
    DateTime StartTime,
    int PlannedDurationMinutes,
    string Message,
    SessionType SessionType
);

public record CompleteSessionResponseDto(
    Guid SessionId,
    DateTime EndTime,
    int ActualDurationMinutes,
    string SessionStatus,
    string Message,
    bool TaskCompleted,
    string? AIFeedback = null,
    int? VerificationScore = null
);

public record ActiveSessionDto(
    Guid SessionId,
    Guid TaskId,
    string Title,
    DateTime StartTime,
    int PlannedDurationMinutes,
    int ElapsedMinutes,
    int RemainingMinutes,
    string SessionStatus,
    bool IsOvertime
);

public record ReviewSessionRequest(
    string? SubmittedCode = null,
    string? SubmittedSummary = null,
    string? SubmittedQuizAnswers = null
);

public record ReviewSessionResponseDto(
    Guid SessionId,
    DateTime ReviewTime,
    string? AIFeedback,
    int? VerificationScore,
    string Message
);