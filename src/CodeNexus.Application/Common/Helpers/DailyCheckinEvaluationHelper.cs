using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Common.Helpers;

public static class DailyCheckinEvaluationHelper
{
    public static (string Mood, int Productivity) Evaluate(FocusSession session)
    {
        var productivity = 3;

        if (session.SessionStatus == SessionStatus.CompletedOnTime)
        {
            productivity += 1;
        }
        else if (session.SessionStatus == SessionStatus.CompletedEarly &&
                 session.PlannedDurationMinutes > 0 &&
                 session.ActualDurationMinutes.HasValue &&
                 session.ActualDurationMinutes.Value < session.PlannedDurationMinutes * 0.6)
        {
            productivity -= 1;
        }

        if (session.PlannedDurationMinutes > 0 && session.ActualDurationMinutes.HasValue)
        {
            var timeRatio = (double)session.ActualDurationMinutes.Value / session.PlannedDurationMinutes;
            if (timeRatio >= 0.9)
            {
                productivity += 1;
            }
            else if (timeRatio < 0.5)
            {
                productivity -= 1;
            }
        }

        if (session.ActualDurationMinutes.HasValue && session.ActualDurationMinutes.Value > 0)
        {
            var pauseRatio = (double)session.TotalPausedMinutes / session.ActualDurationMinutes.Value;
            if (pauseRatio <= 0.1)
            {
                productivity += 1;
            }
            else if (pauseRatio > 0.3)
            {
                productivity -= 1;
            }
        }

        if (session.VerificationScore.HasValue)
        {
            if (session.VerificationScore.Value >= 85)
            {
                productivity += 1;
            }
            else if (session.VerificationScore.Value < 50)
            {
                productivity -= 1;
            }
        }

        if (session.SessionStatus == SessionStatus.Abandoned)
        {
            productivity = 1;
        }

        productivity = Math.Clamp(productivity, 1, 5);

        return (MapMood(productivity), productivity);
    }

    public static (string Mood, int Productivity) EvaluateQuizAttempt(bool passed, decimal percentage)
    {
        var productivity = 2;

        if (passed)
        {
            productivity = percentage >= 90 ? 5 : percentage >= 75 ? 4 : 3;
        }
        else if (percentage < 40)
        {
            productivity = 1;
        }

        productivity = Math.Clamp(productivity, 1, 5);
        return (MapMood(productivity), productivity);
    }

    public static (string Mood, int Productivity) EvaluateLessonRead(bool alreadyRead)
    {
        var productivity = alreadyRead ? 2 : 3;
        return (MapMood(productivity), productivity);
    }

    public static (string Mood, int Productivity) Merge(int? existingProductivity, int newProductivity)
    {
        if (!existingProductivity.HasValue)
        {
            var clamped = Math.Clamp(newProductivity, 1, 5);
            return (MapMood(clamped), clamped);
        }

        var merged = (int)Math.Round((existingProductivity.Value + newProductivity) / 2.0, MidpointRounding.AwayFromZero);
        merged = Math.Clamp(merged, 1, 5);

        return (MapMood(merged), merged);
    }

    private static string MapMood(int productivity)
    {
        return productivity switch
        {
            5 => "Motivated",
            4 => "Focused",
            3 => "Neutral",
            2 => "Tired",
            _ => "Frustrated"
        };
    }
}
