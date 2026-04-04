using System.Collections.Generic;

namespace CodeNexus.Application.Features.DailyCheckin.DTOs;

public record DailyCheckinDto(
    Guid CheckinId,
    Guid UserId,
    DateTime CheckinDate,
    string? Mood,
    int? Productivity,
    DateTime CreatedAt
);

public record GetMyDailyCheckinsRequest(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageNumber = 1,
    int PageSize = 20
);

public record DailyCheckinStatsDto(
    bool TodayCheckedIn,
    int CurrentStreak,
    int LongestStreak,
    int TotalCheckins,
    DateTime? LastCheckinDate,
    bool IsStreakMilestone,
    string PopupCode,
    Dictionary<string, string>? PopupParams
);

public record DailyCheckinStatusDto(
    bool TodayCheckedIn,
    int CurrentStreak
);
