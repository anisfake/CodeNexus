using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetMyTodayDailyCheckin;

public class GetMyTodayDailyCheckinQueryHandler : IRequestHandler<GetMyTodayDailyCheckinQuery, Result<DailyCheckinDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyTodayDailyCheckinQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DailyCheckinDto>> Handle(GetMyTodayDailyCheckinQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == Guid.Empty)
            {
                return Result<DailyCheckinDto>.Failure("UNAUTHORIZED", "User context is invalid.");
            }

            var today = VietnamDateTimeHelper.GetTodayDate();

            var raw = await _context.DailyCheckins
                .AsNoTracking()
                .Where(dc => dc.UserId == userId && dc.CheckinDate == today)
                .OrderByDescending(dc => dc.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (raw == null)
            {
                return Result<DailyCheckinDto>.Failure("DAILY_CHECKIN_NOT_FOUND", "Daily check-in not found.");
            }

            var checkin = new DailyCheckinDto(
                raw.CheckinId,
                raw.UserId,
                raw.CheckinDate,
                raw.Mood,
                DailyCheckinEvaluationHelper.MapProductivityKey(raw.Productivity),
                raw.CreatedAt
            );

            if (checkin == null)
            {
                return Result<DailyCheckinDto>.Failure("DAILY_CHECKIN_NOT_FOUND", "Daily check-in not found.");
            }

            return Result<DailyCheckinDto>.Success(checkin);
        }
        catch (Exception ex)
        {
            return Result<DailyCheckinDto>.Failure(
                "GET_DAILY_CHECKIN_FAILED",
                $"An error occurred while retrieving daily check-in: {ex.Message}");
        }
    }
}
