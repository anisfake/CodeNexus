using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckinStatus;

public class GetMyDailyCheckinStatusQueryHandler : IRequestHandler<GetMyDailyCheckinStatusQuery, Result<DailyCheckinStatusDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyDailyCheckinStatusQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DailyCheckinStatusDto>> Handle(GetMyDailyCheckinStatusQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == Guid.Empty)
            {
                return Result<DailyCheckinStatusDto>.Failure("UNAUTHORIZED", "User context is invalid.");
            }

            var checkinDates = await _context.DailyCheckins
                .AsNoTracking()
                .Where(dc => dc.UserId == userId)
                .Select(dc => dc.CheckinDate.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToListAsync(cancellationToken);

            var today = VietnamDateTimeHelper.GetTodayDate();
            var todayCheckedIn = checkinDates.Contains(today);
            var currentStreak = CalculateCurrentStreak(checkinDates, today);

            return Result<DailyCheckinStatusDto>.Success(new DailyCheckinStatusDto(todayCheckedIn, currentStreak));
        }
        catch (Exception ex)
        {
            return Result<DailyCheckinStatusDto>.Failure(
                "GET_DAILY_CHECKIN_STATUS_FAILED",
                $"An error occurred while retrieving daily check-in status: {ex.Message}");
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
}
