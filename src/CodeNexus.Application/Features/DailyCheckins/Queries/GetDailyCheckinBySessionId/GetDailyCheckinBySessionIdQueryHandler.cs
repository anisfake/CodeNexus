using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DailyCheckin.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DailyCheckin.Queries.GetDailyCheckinBySessionId;

public class GetDailyCheckinBySessionIdQueryHandler : IRequestHandler<GetDailyCheckinBySessionIdQuery, Result<DailyCheckinDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetDailyCheckinBySessionIdQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DailyCheckinDto>> Handle(GetDailyCheckinBySessionIdQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();
            if (userId == Guid.Empty)
            {
                return Result<DailyCheckinDto>.Failure("UNAUTHORIZED", "User context is invalid.");
            }

            var checkin = await _context.DailyCheckins
                .AsNoTracking()
                .Where(dc => dc.SessionId == request.SessionId &&
                             dc.FocusSession.Task.LearningPath.UserId == userId)
                .Select(dc => new DailyCheckinDto(
                    dc.CheckinId,
                    dc.SessionId,
                    dc.CheckinDate,
                    dc.Mood,
                    dc.Productivity,
                    dc.CreatedAt
                ))
                .FirstOrDefaultAsync(cancellationToken);

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
