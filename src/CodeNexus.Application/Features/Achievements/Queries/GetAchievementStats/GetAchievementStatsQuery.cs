using CodeNexus.Application.Features.Achievements.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Achievements.Queries.GetAchievementStats
{
    public class GetAchievementStatsQuery : IRequest<AchievementStatsDto>
    {
        public Guid UserId { get; set; }
    }
}