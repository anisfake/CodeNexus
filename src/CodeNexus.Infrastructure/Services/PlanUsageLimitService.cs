using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
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
        if (await IsPlanLimitExemptRoleAsync(userId, cancellationToken))
            return Result.Success();

        var plan = await _subscriptionAccessService.GetEffectivePlanAsync(userId, cancellationToken);
        var limit = await ResolveLimitAsync(plan, SubscriptionFeatureKey.LearningPathCreation, cancellationToken);
        if (!limit.IsEnabled || !limit.LimitCount.HasValue)
            return Result.Success();

        var used = await CountFeatureUsageLogAsync(userId, SubscriptionFeatureKey.LearningPathCreation, limit.WindowType, cancellationToken);
        return used >= limit.LimitCount.Value
            ? Result.Failure(
                "LEARNING_PATH_LIMIT_EXCEEDED",
                $"{plan.Name} plan allows up to {limit.LimitCount.Value} learning paths per {WindowLabel(limit.WindowType)}.")
            : Result.Success();
    }

    public async Task<Result> CheckTutorMessageAllowedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await IsPlanLimitExemptRoleAsync(userId, cancellationToken))
            return Result.Success();

        var plan = await _subscriptionAccessService.GetEffectivePlanAsync(userId, cancellationToken);
        var limit = await ResolveLimitAsync(plan, SubscriptionFeatureKey.TutorMessages, cancellationToken);
        if (!limit.IsEnabled || !limit.LimitCount.HasValue)
            return Result.Success();

        var used = await CountFeatureUsageLogAsync(userId, SubscriptionFeatureKey.TutorMessages, limit.WindowType, cancellationToken);
        return used >= limit.LimitCount.Value
            ? Result.Failure(
                "TUTOR_MESSAGE_LIMIT_EXCEEDED",
                $"{plan.Name} plan allows up to {limit.LimitCount.Value} tutor messages per {WindowLabel(limit.WindowType)}.")
            : Result.Success();
    }

    public async Task RecordLearningPathCreationUsageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _context.FeatureUsageLogs.AddAsync(new FeatureUsageLog
        {
            FeatureUsageLogId = NewId.NextGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.LearningPathCreation,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task RecordTutorMessageUsageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _context.FeatureUsageLogs.AddAsync(new FeatureUsageLog
        {
            FeatureUsageLogId = NewId.NextGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.TutorMessages,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task<Result> CheckFocusSessionReviewAllowedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await IsPlanLimitExemptRoleAsync(userId, cancellationToken))
            return Result.Success();

        var plan = await _subscriptionAccessService.GetEffectivePlanAsync(userId, cancellationToken);
        var limit = await ResolveLimitAsync(plan, SubscriptionFeatureKey.FocusSessionReview, cancellationToken);
        if (!limit.IsEnabled || !limit.LimitCount.HasValue)
            return Result.Success();

        var used = await CountFeatureUsageLogAsync(userId, SubscriptionFeatureKey.FocusSessionReview, limit.WindowType, cancellationToken);
        return used >= limit.LimitCount.Value
            ? Result.Failure(
                "FOCUS_REVIEW_LIMIT_EXCEEDED",
                $"{plan.Name} plan allows up to {limit.LimitCount.Value} focus review requests per {WindowLabel(limit.WindowType)}.")
            : Result.Success();
    }

    public async Task RecordFocusSessionReviewUsageAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _context.FeatureUsageLogs.AddAsync(new FeatureUsageLog
        {
            FeatureUsageLogId = NewId.NextGuid(),
            UserId = userId,
            FeatureKey = SubscriptionFeatureKey.FocusSessionReview,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    private async Task<PlanLimitSetting> ResolveLimitAsync(
        SubscriptionPlan plan,
        SubscriptionFeatureKey featureKey,
        CancellationToken cancellationToken)
    {
        var configured = await _context.SubscriptionPlanLimits
            .AsNoTracking()
            .Where(x => x.SubscriptionPlanId == plan.SubscriptionPlanId && x.FeatureKey == featureKey)
            .Select(x => new PlanLimitSetting(x.LimitCount, x.WindowType, x.IsEnabled))
            .FirstOrDefaultAsync(cancellationToken);

        return configured ?? BuildFallbackLimit(plan.PlanType, featureKey);
    }

    private async Task<int> CountFeatureUsageLogAsync(
        Guid userId,
        SubscriptionFeatureKey featureKey,
        UsageWindowType windowType,
        CancellationToken cancellationToken)
    {
        var windowStartUtc = GetWindowStartUtc(windowType);
        return windowStartUtc.HasValue
            ? await _context.FeatureUsageLogs
                .AsNoTracking()
                .CountAsync(x => x.UserId == userId && x.FeatureKey == featureKey && x.CreatedAt >= windowStartUtc.Value, cancellationToken)
            : await _context.FeatureUsageLogs
                .AsNoTracking()
                .CountAsync(x => x.UserId == userId && x.FeatureKey == featureKey, cancellationToken);
    }

    private static DateTime? GetWindowStartUtc(UsageWindowType windowType)
    {
        if (windowType == UsageWindowType.Lifetime)
            return null;

        var timezone = ResolveVietnamTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
        var startLocal = windowType == UsageWindowType.Daily
            ? new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified)
            : new DateTime(nowLocal.Year, nowLocal.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);

        return TimeZoneInfo.ConvertTimeToUtc(startLocal, timezone);
    }

    private static string WindowLabel(UsageWindowType windowType)
    {
        return windowType switch
        {
            UsageWindowType.Daily => "day",
            UsageWindowType.Monthly => "month",
            _ => "lifetime"
        };
    }

    private static PlanLimitSetting BuildFallbackLimit(SubscriptionPlanType planType, SubscriptionFeatureKey featureKey)
    {
        return (planType, featureKey) switch
        {
            (SubscriptionPlanType.Free, SubscriptionFeatureKey.LearningPathCreation) => new PlanLimitSetting(4, UsageWindowType.Lifetime, true),
            (SubscriptionPlanType.Standard, SubscriptionFeatureKey.LearningPathCreation) => new PlanLimitSetting(10, UsageWindowType.Monthly, true),
            (SubscriptionPlanType.Pro, SubscriptionFeatureKey.LearningPathCreation) => new PlanLimitSetting(50, UsageWindowType.Monthly, true),

            (SubscriptionPlanType.Free, SubscriptionFeatureKey.TutorMessages) => new PlanLimitSetting(30, UsageWindowType.Daily, true),
            (SubscriptionPlanType.Standard, SubscriptionFeatureKey.TutorMessages) => new PlanLimitSetting(500, UsageWindowType.Monthly, true),
            (SubscriptionPlanType.Pro, SubscriptionFeatureKey.TutorMessages) => new PlanLimitSetting(2000, UsageWindowType.Monthly, true),

            (SubscriptionPlanType.Free, SubscriptionFeatureKey.FocusSessionReview) => new PlanLimitSetting(20, UsageWindowType.Daily, true),
            (SubscriptionPlanType.Standard, SubscriptionFeatureKey.FocusSessionReview) => new PlanLimitSetting(300, UsageWindowType.Monthly, true),
            (SubscriptionPlanType.Pro, SubscriptionFeatureKey.FocusSessionReview) => new PlanLimitSetting(1000, UsageWindowType.Monthly, true),

            _ => new PlanLimitSetting(null, UsageWindowType.Monthly, true)
        };
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

    private async Task<bool> IsPlanLimitExemptRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roleName = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Role != null ? u.Role.RoleName : null)
            .FirstOrDefaultAsync(cancellationToken);

        return string.Equals(roleName, "Mentor", StringComparison.OrdinalIgnoreCase)
               || string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PlanLimitSetting(int? LimitCount, UsageWindowType WindowType, bool IsEnabled);
}
