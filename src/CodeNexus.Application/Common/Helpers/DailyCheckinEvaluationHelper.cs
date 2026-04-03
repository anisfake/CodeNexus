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

        var mood = productivity switch
        {
            5 => "Motivated",
            4 => "Focused",
            3 => "Neutral",
            2 => "Tired",
            _ => "Frustrated"
        };

        if (session.SessionStatus == SessionStatus.Abandoned)
        {
            mood = "Frustrated";
        }

        return (mood, productivity);
    }
}
