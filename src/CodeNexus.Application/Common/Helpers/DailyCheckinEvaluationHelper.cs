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
            null or 0   => null,
            1           => "productivity.first_step",
            2           => "productivity.warming_up",
            3           => "productivity.keep_going",
            4           => "productivity.building_momentum",
            5           => "productivity.halfway_there",
            6           => "productivity.good_progress",
            7           => "productivity.strong_effort",
            8           => "productivity.on_fire",
            9           => "productivity.almost_outstanding",
            10          => "productivity.excellent_today",
            11          => "productivity.super_productive",
            12          => "productivity.unstoppable",
            13          => "productivity.learning_machine",
            14          => "productivity.legendary_focus",
            _           => "productivity.maximum_overdrive"
        };
    }
}

