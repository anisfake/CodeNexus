namespace CodeNexus.Application.Common.Interfaces;

public interface IAchievementHelperService
{
    Task CheckConsistentAchievementAsync(Guid userId);
    Task CheckMultiTaskerAchievementAsync(Guid userId);
}