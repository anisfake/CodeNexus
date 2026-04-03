using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckinStats;

public class GetMyDailyCheckinStatsQueryHandler : IRequestHandler<GetMyDailyCheckinStatsQuery, Result<DailyCheckinStatsDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyDailyCheckinStatsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DailyCheckinStatsDto>> Handle(GetMyDailyCheckinStatsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == Guid.Empty)
            {
                return Result<DailyCheckinStatsDto>.Failure("UNAUTHORIZED", "User context is invalid.");
            }

            var checkinDates = await _context.DailyCheckins
                .AsNoTracking()
                .Where(dc => dc.UserId == userId)
                .Select(dc => dc.CheckinDate.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToListAsync(cancellationToken);

            var today = DateTime.UtcNow.Date;
            var todayCheckedIn = checkinDates.Contains(today);
            var currentStreak = CalculateCurrentStreak(checkinDates, today);
            var longestStreak = CalculateLongestStreak(checkinDates);
            var isStreakMilestone = IsStreakMilestone(currentStreak);
            var (popupCode, popupParams) = BuildPopupPayload(todayCheckedIn, currentStreak, isStreakMilestone);

            var result = new DailyCheckinStatsDto(
                TodayCheckedIn: todayCheckedIn,
                CurrentStreak: currentStreak,
                LongestStreak: longestStreak,
                TotalCheckins: checkinDates.Count,
                LastCheckinDate: checkinDates.Count > 0 ? checkinDates[0] : null,
                IsStreakMilestone: isStreakMilestone,
                PopupCode: popupCode,
                PopupParams: popupParams
            );

            return Result<DailyCheckinStatsDto>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<DailyCheckinStatsDto>.Failure(
                "GET_DAILY_CHECKIN_STATS_FAILED",
                $"An error occurred while retrieving daily check-in stats: {ex.Message}");
        }
    }

    private static int CalculateCurrentStreak(List<DateTime> sortedDatesDesc, DateTime today)
    {
        if (sortedDatesDesc.Count == 0)
        {
            return 0;
        }

        var latest = sortedDatesDesc[0];
        if (latest != today && latest != today.AddDays(-1))
        {
            return 0;
        }

        var streak = 0;
        var expectedDate = latest;

        foreach (var date in sortedDatesDesc)
        {
            if (date == expectedDate)
            {
                streak++;
                expectedDate = expectedDate.AddDays(-1);
            }
            else
            {
                break;
            }
        }

        return streak;
    }

    private static int CalculateLongestStreak(List<DateTime> sortedDatesDesc)
    {
        if (sortedDatesDesc.Count == 0)
        {
            return 0;
        }

        var sortedAsc = sortedDatesDesc.OrderBy(d => d).ToList();
        var longest = 1;
        var current = 1;

        for (var i = 1; i < sortedAsc.Count; i++)
        {
            if (sortedAsc[i] == sortedAsc[i - 1].AddDays(1))
            {
                current++;
                if (current > longest)
                {
                    longest = current;
                }
            }
            else
            {
                current = 1;
            }
        }

        return longest;
    }

    private static bool IsStreakMilestone(int currentStreak)
    {
        return currentStreak is 3 or 7 or 14 or 30 or 60 or 100;
    }

    private static (string PopupCode, Dictionary<string, string>? PopupParams) BuildPopupPayload(
        bool todayCheckedIn,
        int currentStreak,
        bool isStreakMilestone)
    {
        if (!todayCheckedIn)
        {
            return ("DAILY_CHECKIN_NOT_DONE_TODAY", null);
        }

        if (isStreakMilestone)
        {
            return (
                "DAILY_CHECKIN_STREAK_MILESTONE",
                new Dictionary<string, string>
                {
                    ["currentStreak"] = currentStreak.ToString()
                });
        }

        if (currentStreak > 1)
        {
            return (
                "DAILY_CHECKIN_DONE_TODAY_STREAK",
                new Dictionary<string, string>
                {
                    ["currentStreak"] = currentStreak.ToString()
                });
        }

        return ("DAILY_CHECKIN_DONE_TODAY", null);
    }
}
