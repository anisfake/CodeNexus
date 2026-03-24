using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Achievements.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Achievements.Queries.GetUserAchievements
{
    public class GetUserAchievementsQueryHandler : IRequestHandler<GetUserAchievementsQuery, List<UserAchievementDto>>
    {
        private readonly IApplicationDbContext _context;

        public GetUserAchievementsQueryHandler(IApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<UserAchievementDto>> Handle(GetUserAchievementsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.UserAchievements
                .Include(ua => ua.Achievement)
                .Where(ua => ua.UserId == request.UserId);

            if (request.UnlockedOnly)
            {
                query = query.Where(ua => ua.IsUnlocked);
            }

            if (!string.IsNullOrEmpty(request.Category) && Enum.TryParse<AchievementCategory>(request.Category, out var category))
            {
                query = query.Where(ua => ua.Achievement.Category == category);
            }

            var userAchievements = await query
                .OrderByDescending(ua => ua.IsUnlocked)
                .ThenByDescending(ua => ua.UnlockedAt)
                .ThenBy(ua => ua.Achievement.SortOrder)
                .ToListAsync(cancellationToken);

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
    }
}