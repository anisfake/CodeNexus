using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIAccessPolicy;
using CodeNexus.Application.Features.SystemRuntimePolicies;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Infrastructure.Services;

public class PlanUsageLimitService : IPlanUsageLimitService
{
    private readonly IApplicationDbContext _context;

    public PlanUsageLimitService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result> CheckLearningPathCreationAllowedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessProfileAsync(userId, cancellationToken);
        if (access.IsExemptRole || access.IsPaidUser)
            return Result.Success();

        var policy = await LoadFreeUsagePolicyAsync(cancellationToken);
        var used = await CountFeatureUsageLogAsync(userId, SubscriptionFeatureKey.LearningPathCreation, UsageWindowType.Monthly, cancellationToken);
        return used >= policy.FreeLearningPathMonthlyLimit
            ? Result.Failure(
                "LEARNING_PATH_LIMIT_EXCEEDED",
                $"Free user chi duoc tao toi da {policy.FreeLearningPathMonthlyLimit} learning path moi thang.")
            : Result.Success();
    }

    public async Task<Result> CheckTutorMessageAllowedAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var access = await ResolveAccessProfileAsync(userId, cancellationToken);
        if (access.IsExemptRole || access.IsPaidUser)
            return Result.Success();

        var policy = await LoadFreeUsagePolicyAsync(cancellationToken);
        var used = await CountFeatureUsageLogAsync(userId, SubscriptionFeatureKey.TutorMessages, UsageWindowType.Monthly, cancellationToken);
        return used >= policy.FreeTutorMessagesMonthlyLimit
            ? Result.Failure(
                "TUTOR_MESSAGE_LIMIT_EXCEEDED",
                $"Free user chi duoc dung toi da {policy.FreeTutorMessagesMonthlyLimit} tutor messages moi thang.")
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
        var access = await ResolveAccessProfileAsync(userId, cancellationToken);
        if (access.IsExemptRole || access.IsPaidUser)
            return Result.Success();

        var policy = await LoadFreeUsagePolicyAsync(cancellationToken);
        var used = await CountFeatureUsageLogAsync(userId, SubscriptionFeatureKey.FocusSessionReview, UsageWindowType.Monthly, cancellationToken);
        return used >= policy.FreeFocusSessionReviewMonthlyLimit
            ? Result.Failure(
                "FOCUS_REVIEW_LIMIT_EXCEEDED",
                $"Free user chi duoc dung toi da {policy.FreeFocusSessionReviewMonthlyLimit} AI review moi thang.")
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

    private async Task<UsageAccessProfile> ResolveAccessProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                RoleName = u.Role != null ? u.Role.RoleName : null,
                u.BalanceVnd
            })
            .FirstOrDefaultAsync(cancellationToken);

        var roleName = profile?.RoleName;
        var isExemptRole = string.Equals(roleName, "Mentor", StringComparison.OrdinalIgnoreCase)
                           || string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);
        var isPaidUser = (profile?.BalanceVnd ?? 0m) > 0m;
        return new UsageAccessProfile(isExemptRole, isPaidUser);
    }

    private async Task<FreeUsagePolicy> LoadFreeUsagePolicyAsync(CancellationToken cancellationToken)
    {
        const int unlimited = int.MaxValue;

        try
        {
            var policy = await _context.SystemRuntimePolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.PolicyKey == FreeUsagePolicyConstants.PolicyKey && x.IsActive,
                    cancellationToken);

            if (policy != null)
            {
                var config = SystemRuntimePolicyJsonHelper.ParseConfigJson(policy.ConfigJson);
                var learningPathLimit = ReadPolicyInt(config, FreeUsagePolicyConstants.LearningPathMonthlyLimitConfigKey, unlimited);
                var tutorMessageLimit = ReadPolicyInt(config, FreeUsagePolicyConstants.TutorMessagesMonthlyLimitConfigKey, unlimited);
                var focusReviewLimit = ReadPolicyInt(config, FreeUsagePolicyConstants.FocusSessionReviewMonthlyLimitConfigKey, unlimited);

                return new FreeUsagePolicy(learningPathLimit, tutorMessageLimit, focusReviewLimit);
            }
        }
        catch
        {
            // fallback when policy table not ready
        }

        return new FreeUsagePolicy(
            unlimited,
            unlimited,
            unlimited);
    }

    private static int ReadPolicyInt(
        IReadOnlyDictionary<string, object> config,
        string key,
        int fallbackValue)
    {
        if (!config.TryGetValue(key, out var rawValue))
        {
            return fallbackValue;
        }

        var parsed = rawValue switch
        {
            int value => value,
            long value when value is <= int.MaxValue and >= int.MinValue => (int)value,
            double value when value is <= int.MaxValue and >= int.MinValue => (int)value,
            decimal value when value is <= int.MaxValue and >= int.MinValue => (int)value,
            string text when int.TryParse(text, out var value) => value,
            _ => fallbackValue
        };

        return parsed < 0 ? fallbackValue : parsed;
    }

    private sealed record UsageAccessProfile(bool IsExemptRole, bool IsPaidUser);

    private sealed record FreeUsagePolicy(
        int FreeLearningPathMonthlyLimit,
        int FreeTutorMessagesMonthlyLimit,
        int FreeFocusSessionReviewMonthlyLimit);
}
