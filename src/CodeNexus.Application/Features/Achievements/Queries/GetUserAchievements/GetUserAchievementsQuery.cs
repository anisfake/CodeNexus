using CodeNexus.Application.Features.Achievements.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Achievements.Queries.GetUserAchievements
{
    public class GetUserAchievementsQuery : IRequest<List<UserAchievementDto>>
    {
        public Guid UserId { get; set; }
        public bool UnlockedOnly { get; set; } = false;
        public string? Category { get; set; }
    }
}