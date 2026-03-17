using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Achievements.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Achievements.Queries.GetAchievementStats
{
    public class GetAchievementStatsQueryHandler : IRequestHandler<GetAchievementStatsQuery, AchievementStatsDto>
    {
        private readonly IApplicationDbContext _context;

        public GetAchievementStatsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AchievementStatsDto> Handle(GetAchievementStatsQuery request, CancellationToken cancellationToken)
        {
            var userAchievements = await _context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == request.UserId)
                .ToListAsync(cancellationToken);

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
    }
}