using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using DailyCheckinEntity = CodeNexus.Domain.Entities.DailyCheckins;

namespace CodeNexus.Application.Features.DailyCheckin.Commands.SetDailyMood;

public class SetDailyMoodCommandHandler : IRequestHandler<SetDailyMoodCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SetDailyMoodCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(SetDailyMoodCommand request, CancellationToken cancellationToken)
    {
        Guid userId;
        try
        {
            userId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var today = VietnamDateTimeHelper.GetTodayDate();
        var existing = await _context.DailyCheckins
            .FirstOrDefaultAsync(x => x.UserId == userId && x.CheckinDate == today, cancellationToken);

        if (existing == null)
        {
            _context.DailyCheckins.Add(new DailyCheckinEntity
            {
                CheckinId = NewId.NextGuid(),
                UserId = userId,
                CheckinDate = today,
                Mood = request.Mood,
                CreatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Mood = request.Mood;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
