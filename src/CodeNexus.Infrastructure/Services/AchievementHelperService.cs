using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Infrastructure.Services;

public class AchievementHelperService : IAchievementHelperService
{
    private readonly IApplicationDbContext _context;
    private readonly IAchievementService _achievementService;

    public AchievementHelperService(IApplicationDbContext context, IAchievementService achievementService)
    {
        _context = context;
        _achievementService = achievementService;
    }

    public async Task CheckConsistentAchievementAsync(Guid userId)
    {
        // Check if user completed focus sessions 3 days in a row
        var today = DateTime.UtcNow.Date;
        var threeDaysAgo = today.AddDays(-2);

        var completedDays = await _context.FocusSessions
            .Include(fs => fs.Task)
                .ThenInclude(t => t.LearningPath)
            .Where(fs => fs.Task.LearningPath.UserId == userId &&
                        (fs.SessionStatus == SessionStatus.CompletedOnTime || fs.SessionStatus == SessionStatus.CompletedEarly) &&
                        fs.EndTime.HasValue &&
                        fs.EndTime.Value.Date >= threeDaysAgo &&
                        fs.EndTime.Value.Date <= today)
            .Select(fs => fs.EndTime!.Value.Date)
            .Distinct()
            .CountAsync();

        if (completedDays >= 3)
        {
            await _achievementService.TryUnlockAsync(userId, "consistent");
        }
    }

    public async Task CheckMultiTaskerAchievementAsync(Guid userId)
    {
        // Check if user has 3 or more active goals
        var activeGoalsCount = await _context.Goals
            .CountAsync(g => g.CreatedByUserId == userId);

        if (activeGoalsCount >= 3)
        {
            await _achievementService.TryUnlockAsync(userId, "multi_tasker");
        }
    }
}