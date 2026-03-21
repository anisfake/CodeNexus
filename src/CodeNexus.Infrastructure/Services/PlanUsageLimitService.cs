using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Infrastructure.Services;

public class PlanUsageLimitService : IPlanUsageLimitService
{
    private readonly IApplicationDbContext _context;
    private readonly ISubscriptionAccessService _subscriptionAccessService;

    public PlanUsageLimitService(
        IApplicationDbContext context,
        ISubscriptionAccessService subscriptionAccessService)
    {
        _context = context;
        _subscriptionAccessService = subscriptionAccessService;
    }

    public async Task<Result> CheckLearningPathCreationAllowedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await _subscriptionAccessService.GetEffectivePlanAsync(userId, cancellationToken);

        if (plan.PlanType == SubscriptionPlanType.Free)
        {
            var totalLearningPaths = await _context.LearningPaths
                .AsNoTracking()
                .CountAsync(lp => lp.UserId == userId, cancellationToken);

            return totalLearningPaths >= 4
                ? Result.Failure(
                    "LEARNING_PATH_LIMIT_EXCEEDED",
                    "Free plan only allows up to 4 learning paths in total. Upgrade your plan to create more.")
                : Result.Success();
        }

        var (windowStartUtc, windowLabel) = GetCurrentMonthlyWindowUtc();
        var monthlyLimit = plan.PlanType switch
        {
            SubscriptionPlanType.Standard => 10,
            SubscriptionPlanType.Pro => 50,
            _ => 0
        };

        if (monthlyLimit <= 0)
        {
            return Result.Success();
        }

        var monthlyLearningPaths = await _context.LearningPaths
            .AsNoTracking()
            .CountAsync(lp => lp.UserId == userId && lp.CreatedAt >= windowStartUtc, cancellationToken);

        return monthlyLearningPaths >= monthlyLimit
            ? Result.Failure(
                "LEARNING_PATH_LIMIT_EXCEEDED",
                $"{plan.Name} plan allows up to {monthlyLimit} learning paths per {windowLabel}.")
            : Result.Success();
    }

    public async Task<Result> CheckTutorMessageAllowedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await _subscriptionAccessService.GetEffectivePlanAsync(userId, cancellationToken);

        int limit;
        DateTime windowStartUtc;
        string windowLabel;

        if (plan.PlanType == SubscriptionPlanType.Free)
        {
            (windowStartUtc, windowLabel) = GetCurrentDailyWindowUtc();
            limit = 30;
        }
        else
        {
            (windowStartUtc, windowLabel) = GetCurrentMonthlyWindowUtc();
            limit = plan.PlanType switch
            {
                SubscriptionPlanType.Standard => 500,
                SubscriptionPlanType.Pro => 2000,
                _ => 0
            };
        }

        if (limit <= 0)
        {
            return Result.Success();
        }

        var tutorConversationIds = await _context.Conversations
            .AsNoTracking()
            .Where(c => c.UserId == userId && !c.IsDeleted)
            .Select(c => c.ConversationId)
            .ToListAsync(cancellationToken);

        var usedMessages = tutorConversationIds.Count == 0
            ? 0
            : await _context.Messages
                .AsNoTracking()
                .CountAsync(
                    message => tutorConversationIds.Contains(message.ConversationId)
                               && message.CreatedAt >= windowStartUtc
                               && message.Content.StartsWith("USER:"),
                    cancellationToken);

        return usedMessages >= limit
            ? Result.Failure(
                "TUTOR_MESSAGE_LIMIT_EXCEEDED",
                $"{plan.Name} plan allows up to {limit} tutor messages per {windowLabel}.")
            : Result.Success();
    }

    private static (DateTime WindowStartUtc, string WindowLabel) GetCurrentDailyWindowUtc()
    {
        var timezone = ResolveVietnamTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
        var dayStartLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, timezone);
        return (dayStartUtc, "day");
    }

    private static (DateTime WindowStartUtc, string WindowLabel) GetCurrentMonthlyWindowUtc()
    {
        var timezone = ResolveVietnamTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
        var monthStartLocal = new DateTime(nowLocal.Year, nowLocal.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var monthStartUtc = TimeZoneInfo.ConvertTimeToUtc(monthStartLocal, timezone);
        return (monthStartUtc, "month");
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
    }
}
