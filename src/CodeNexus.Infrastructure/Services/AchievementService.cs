using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Achievements.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodeNexus.Infrastructure.Services
{
    public class AchievementService : IAchievementService
    {
        private readonly IApplicationDbContext _context;
        private readonly ILogger<AchievementService> _logger;

        public AchievementService(IApplicationDbContext context, ILogger<AchievementService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task TryUnlockAsync(Guid userId, string achievementKey)
        {
            try
            {
                var userAchievement = await _context.UserAchievements
                    .Include(ua => ua.Achievement)
                    .FirstOrDefaultAsync(ua => ua.UserId == userId && 
                                             ua.Achievement.Name.ToLower().Contains(achievementKey.ToLower()) &&
                                             !ua.IsUnlocked);

                if (userAchievement != null)
                {
                    userAchievement.IsUnlocked = true;
                    userAchievement.UnlockedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Achievement '{AchievementName}' unlocked for user {UserId}", 
                        userAchievement.Achievement.Name, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unlocking achievement {AchievementKey} for user {UserId}", achievementKey, userId);
            }
        }

        public async Task<AchievementStatsDto> GetUserAchievementStatsAsync(Guid userId)
        {
            var userAchievements = await _context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == userId)
                .ToListAsync();

            var totalAchievements = userAchievements.Count;
            var unlockedAchievements = userAchievements.Count(ua => ua.IsUnlocked);
            var totalPoints = userAchievements.Where(ua => ua.IsUnlocked).Sum(ua => ua.Achievement.Points);

            var currentLevel = totalPoints / 1000 + 1;
            var pointsToNextLevel = 1000 - (totalPoints % 1000);

            var recentUnlocked = userAchievements
                .Where(ua => ua.IsUnlocked && ua.UnlockedAt.HasValue)
                .OrderByDescending(ua => ua.UnlockedAt)
                .Take(5)
                .Select(ua => new UserAchievementDto(
                    ua.UserAchievementId,
                    ua.AchievementId,
                    ua.Achievement.Name,
                    ua.Achievement.Description,
                    ua.Achievement.Icon,
                    ua.Achievement.Category,
                    ua.Achievement.Points,
                    ua.IsUnlocked,
                    ua.UnlockedAt
                )).ToList();

            return new AchievementStatsDto(
                totalAchievements,
                unlockedAchievements,
                totalPoints,
                currentLevel,
                pointsToNextLevel,
                recentUnlocked
            );
        }

        public async Task<List<UserAchievementDto>> GetUserAchievementsAsync(Guid userId, bool unlockedOnly = false)
        {
            var query = _context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == userId);

            if (unlockedOnly)
                query = query.Where(ua => ua.IsUnlocked);

            var userAchievements = await query.ToListAsync();

            return userAchievements.Select(ua => new UserAchievementDto(
                ua.UserAchievementId,
                ua.AchievementId,
                ua.Achievement.Name,
                ua.Achievement.Description,
                ua.Achievement.Icon,
                ua.Achievement.Category,
                ua.Achievement.Points,
                ua.IsUnlocked,
                ua.UnlockedAt
            )).ToList();
        }

        public async Task<List<UserAchievementDto>> GetUserAchievementsByCategoryAsync(Guid userId, string category)
        {
            if (!Enum.TryParse<AchievementCategory>(category, out var categoryEnum))
                return new List<UserAchievementDto>();

            var userAchievements = await _context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == userId && ua.Achievement.Category == categoryEnum)
                .ToListAsync();

            return userAchievements.Select(ua => new UserAchievementDto(
                ua.UserAchievementId,
                ua.AchievementId,
                ua.Achievement.Name,
                ua.Achievement.Description,
                ua.Achievement.Icon,
                ua.Achievement.Category,
                ua.Achievement.Points,
                ua.IsUnlocked,
                ua.UnlockedAt
            )).ToList();
        }

        public async Task<List<AchievementDto>> GetAllAchievementsAsync()
        {
            var achievements = await _context.Achievements
                .Where(a => a.IsActive)
                .OrderBy(a => a.Category)
                .ThenBy(a => a.SortOrder)
                .ToListAsync();

            return achievements.Select(a => new AchievementDto(
                a.AchievementId,
                a.Name,
                a.Description,
                a.Icon,
                a.Category,
                a.Points,
                a.IsActive
            )).ToList();
        }

        public async Task InitializeUserAchievementsAsync(Guid userId)
        {
            var achievements = await _context.Achievements
                .Where(a => a.IsActive)
                .ToListAsync();

            var existingUserAchievements = await _context.UserAchievements
                .Where(ua => ua.UserId == userId)
                .Select(ua => ua.AchievementId)
                .ToListAsync();

            var newUserAchievements = achievements
                .Where(a => !existingUserAchievements.Contains(a.AchievementId))
                .Select(a => new UserAchievement
                {
                    UserAchievementId = Guid.NewGuid(),
                    UserId = userId,
                    AchievementId = a.AchievementId,
                    IsUnlocked = false,
                    CreatedAt = DateTime.UtcNow
                }).ToList();

            if (newUserAchievements.Any())
            {
                _context.UserAchievements.AddRange(newUserAchievements);
                await _context.SaveChangesAsync();
            }
        }
    }
}