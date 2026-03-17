using CodeNexus.Application.Features.Achievements.DTOs;
using CodeNexus.Application.Features.Achievements.Queries.GetAchievementStats;
using CodeNexus.Application.Features.Achievements.Queries.GetUserAchievements;
using CodeNexus.Application.Common.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CodeNexus.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AchievementController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IAchievementService _achievementService;

        public AchievementController(IMediator mediator, IAchievementService achievementService)
        {
            _mediator = mediator;
            _achievementService = achievementService;
        }

        [HttpGet("my-achievements")]
        public async Task<ActionResult<List<UserAchievementDto>>> GetMyAchievements(
            [FromQuery] bool unlockedOnly = false,
            [FromQuery] string? category = null)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            var query = new GetUserAchievementsQuery
            {
                UserId = userId,
                UnlockedOnly = unlockedOnly,
                Category = category
            };

            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("stats")]
        public async Task<ActionResult<AchievementStatsDto>> GetAchievementStats()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            var query = new GetAchievementStatsQuery { UserId = userId };
            var result = await _mediator.Send(query);
            
            return Ok(result);
        }

        [HttpGet("all")]
        public async Task<ActionResult<List<AchievementDto>>> GetAllAchievements()
        {
            var result = await _achievementService.GetAllAchievementsAsync();
            return Ok(result);
        }

        [HttpPost("initialize")]
        public async Task<ActionResult> InitializeAchievements()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _achievementService.InitializeUserAchievementsAsync(userId);
            
            return Ok(new { message = "Achievements initialized successfully" });
        }

        [HttpGet("notifications")]
        public async Task<ActionResult<List<AchievementNotificationDto>>> GetAchievementNotifications()
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            
            // Get recently unlocked achievements (last 24 hours)
            var recentlyUnlocked = await _mediator.Send(new GetUserAchievementsQuery 
            { 
                UserId = userId, 
                UnlockedOnly = true 
            });
            
            var notifications = recentlyUnlocked
                .Where(ua => ua.UnlockedAt.HasValue && ua.UnlockedAt.Value > DateTime.UtcNow.AddDays(-1))
                .Select(ua => new AchievementNotificationDto(
                    ua.AchievementId,
                    ua.Name,
                    ua.Description,
                    ua.Icon,
                    ua.Points,
                    ua.UnlockedAt!.Value
                )).ToList();
            
            return Ok(notifications);
        }

        [HttpGet("category/{category}")]
        public async Task<ActionResult<List<UserAchievementDto>>> GetAchievementsByCategory(string category)
        {
            var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _achievementService.GetUserAchievementsByCategoryAsync(userId, category);
            
            return Ok(result);
        }
    }
}