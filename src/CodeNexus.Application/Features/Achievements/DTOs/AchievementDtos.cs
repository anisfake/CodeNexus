using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Achievements.DTOs
{
    public record AchievementDto(
        Guid AchievementId,
        string Name,
        string Description,
        string Icon,
        AchievementCategory Category,
        int Points,
        bool IsActive
    );

    public record UserAchievementDto(
        Guid UserAchievementId,
        Guid AchievementId,
        string Name,
        string Description,
        string Icon,
        AchievementCategory Category,
        int Points,
        bool IsUnlocked,
        DateTime? UnlockedAt
    );

    public record AchievementStatsDto(
        int TotalAchievements,
        int UnlockedAchievements,
        int TotalPoints,
        int CurrentLevel,
        int PointsToNextLevel,
        List<UserAchievementDto> RecentUnlocked
    );

    public record AchievementNotificationDto(
        Guid AchievementId,
        string Name,
        string Description,
        string Icon,
        int Points,
        DateTime UnlockedAt
    );
}