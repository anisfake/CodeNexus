using CodeNexus.Application.Features.Achievements.DTOs;

namespace CodeNexus.Application.Common.Interfaces
{
    public interface IAchievementService
    {
        Task TryUnlockAsync(Guid userId, string achievementKey);
        
        Task<AchievementStatsDto> GetUserAchievementStatsAsync(Guid userId);
        Task<List<UserAchievementDto>> GetUserAchievementsAsync(Guid userId, bool unlockedOnly = false);
        Task<List<UserAchievementDto>> GetUserAchievementsByCategoryAsync(Guid userId, string category);
        Task<List<AchievementDto>> GetAllAchievementsAsync();
        
        Task InitializeUserAchievementsAsync(Guid userId);
    }
}