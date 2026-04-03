using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckins;

public class GetMyDailyCheckinsQueryHandler : IRequestHandler<GetMyDailyCheckinsQuery, Result<List<DailyCheckinDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyDailyCheckinsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<DailyCheckinDto>>> Handle(GetMyDailyCheckinsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == Guid.Empty)
            {
                return Result<List<DailyCheckinDto>>.Failure("UNAUTHORIZED", "User context is invalid.");
            }

            var query = _context.DailyCheckins
                .AsNoTracking()
                .Where(dc => dc.FocusSession.Task.LearningPath.UserId == userId);

            if (request.FromDate.HasValue)
            {
                var fromDate = request.FromDate.Value.Date;
                query = query.Where(dc => dc.CheckinDate >= fromDate);
            }

            if (request.ToDate.HasValue)
            {
                var toDate = request.ToDate.Value.Date;
                query = query.Where(dc => dc.CheckinDate <= toDate);
            }

            var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
            var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

            var checkins = await query
                .OrderByDescending(dc => dc.CheckinDate)
                .ThenByDescending(dc => dc.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(dc => new DailyCheckinDto(
                    dc.CheckinId,
                    dc.SessionId,
                    dc.CheckinDate,
                    dc.Mood,
                    dc.Productivity,
                    dc.CreatedAt
                ))
                .ToListAsync(cancellationToken);

            return Result<List<DailyCheckinDto>>.Success(checkins);
        }
        catch (Exception ex)
        {
            return Result<List<DailyCheckinDto>>.Failure(
                "GET_MY_DAILY_CHECKINS_FAILED",
                $"An error occurred while retrieving daily check-ins: {ex.Message}");
        }
    }
}
