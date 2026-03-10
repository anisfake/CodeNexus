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
    int PlannedDurationMinutes = 25,
    string? Title = null
);

public record CompleteSessionRequest(
    string? SubmittedCode = null,
    string? SubmittedSummary = null,
    bool IsEarlyCompletion = false
);

public record CompleteSessionWithRawCodeRequest(
    string? RawCode = null,
    string? RawSummary = null,
    bool IsEarlyCompletion = false
);

public record StartSessionResponseDto(
    Guid SessionId,
    DateTime StartTime,
    int PlannedDurationMinutes,
    string Message
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