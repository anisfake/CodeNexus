namespace CodeNexus.Application.Features.Dashboard.DTOs;

public record StudentDashboardStatsResponse(
    int TotalLessons,
    int CompletedLessons,
    int TotalChapters,
    int CompletedChapters,
    int TotalLearningPaths,
    int TotalQuizAttempts,
    int TotalStudyMinutes,
    int CurrentStreak
);

public record MentorRecentStudentMessageDto(
    Guid MessageId,
    Guid ConversationId,
    Guid StudentId,
    string StudentName,
    string Content,
    DateTime SentAt
);

public record MentorDashboardOverviewResponse(
    int SupportedStudentsCount,
    int CreatedSubjectsCount,
    int DraftLearningPathsCount,
    List<MentorRecentStudentMessageDto> RecentStudentMessages
);
