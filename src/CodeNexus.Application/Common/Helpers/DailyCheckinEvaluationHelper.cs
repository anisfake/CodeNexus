using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Common.Helpers;

public static class DailyCheckinEvaluationHelper
{
    // Activity increments per event type
    public const int LessonActivityIncrement = 1;
    public const int QuizActivityIncrement = 1;
    public const int SessionActivityIncrement = 2;

    /// <summary>
    /// Returns the new total after incrementing the existing activity counter.
    /// </summary>
    public static int IncrementActivityCount(int? existing, int increment)
    {
        return (existing ?? 0) + increment;
    }

    /// <summary>
    /// Maps a raw activity count to an i18n message key.
    /// Returns null when there is no activity yet.
    /// </summary>
    public static string? MapProductivityKey(int? activityCount)
    {
        return activityCount switch
        {
            null or 0 => null,
            <= 2 => "productivity.keep_going",
            <= 5 => "productivity.good_progress",
            _ => "productivity.excellent_today"
        };
    }
}

