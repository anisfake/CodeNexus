using System.Text.Json;
using System.Text.Json.Serialization;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Lessons.Queries.EstimateBulkLearningPathGenerationBudget;

public class EstimateBulkLearningPathGenerationBudgetQueryHandler
    : IRequestHandler<EstimateBulkLearningPathGenerationBudgetQuery, Result<EstimateBulkLearningPathGenerationBudgetDto>>
{
    private const decimal UpfrontEstimateSafetyMultiplier = 1.10m;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public EstimateBulkLearningPathGenerationBudgetQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<EstimateBulkLearningPathGenerationBudgetDto>> Handle(
        EstimateBulkLearningPathGenerationBudgetQuery request,
        CancellationToken cancellationToken)
    {
        var pendingLessonCount = Math.Max(0, request.PendingLessonCount);
        var pendingQuizCount = Math.Max(0, request.PendingQuizCount);
        var estimatedAiCalls = pendingLessonCount + pendingQuizCount;

        var userId = _currentUserService.GetUserId();
        var userAccess = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                u.TokenBalance,
                RoleName = u.Role != null ? u.Role.RoleName : null
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (userAccess == null)
        {
            return Result<EstimateBulkLearningPathGenerationBudgetDto>.Failure(
                "UNAUTHORIZED",
                "User not authenticated.");
        }

        var isValidationApplied = !IsPrivilegedRole(userAccess.RoleName)
                                  && !IsMentorRole(userAccess.RoleName)
                                  && userAccess.TokenBalance > 0m;

        if (!isValidationApplied || estimatedAiCalls == 0)
        {
            return Result<EstimateBulkLearningPathGenerationBudgetDto>.Success(
                new EstimateBulkLearningPathGenerationBudgetDto(
                    isValidationApplied,
                    true,
                    0m,
                    userAccess.TokenBalance,
                    pendingLessonCount,
                    pendingQuizCount,
                    estimatedAiCalls));
        }

        var paidConfig = await ResolvePaidContentConfigAsync(cancellationToken);
        if (paidConfig == null)
        {
            return Result<EstimateBulkLearningPathGenerationBudgetDto>.Failure(
                "PAID_AI_CONFIG_NOT_FOUND",
                "Paid AI configuration for content generation is missing.");
        }

        var runtimeConfig = ParseRuntimeConfig(paidConfig.ConfigJson);
        var estimatedRequiredTokens = EstimateTotalRequiredTokens(runtimeConfig, pendingLessonCount, pendingQuizCount);
        var isEnoughTokenBalance = userAccess.TokenBalance >= estimatedRequiredTokens;

        return Result<EstimateBulkLearningPathGenerationBudgetDto>.Success(
            new EstimateBulkLearningPathGenerationBudgetDto(
                isValidationApplied,
                isEnoughTokenBalance,
                estimatedRequiredTokens,
                userAccess.TokenBalance,
                pendingLessonCount,
                pendingQuizCount,
                estimatedAiCalls));
    }

    private async Task<AIProviderConfig?> ResolvePaidContentConfigAsync(CancellationToken cancellationToken)
    {
        var byUsage = await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c =>
                c.IsActive
                && c.AccessTier == AIAccessTier.Paid
                && c.UsageType == AIUsageType.ContentGeneration)
            .OrderByDescending(c => c.LastUpdated)
            .ThenBy(c => c.ConfigId)
            .FirstOrDefaultAsync(cancellationToken);

        if (byUsage != null)
        {
            return byUsage;
        }

        return await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c => c.IsActive && c.AccessTier == AIAccessTier.Paid)
            .OrderBy(c => c.UsageType == AIUsageType.ContentGeneration ? 0 : 1)
            .ThenByDescending(c => c.LastUpdated)
            .ThenBy(c => c.ConfigId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private decimal EstimateTotalRequiredTokens(RuntimeConfig runtimeConfig, int pendingLessonCount, int pendingQuizCount)
    {
        if ((runtimeConfig.InputCostPer1M <= 0m && runtimeConfig.OutputCostPer1M <= 0m)
            || (pendingLessonCount <= 0 && pendingQuizCount <= 0))
        {
            return 0m;
        }

        var inputTokens = Math.Max(runtimeConfig.MaxTokens, 512);
        var lessonOutputTokens = Math.Max(runtimeConfig.MaxTokens, 256);
        var quizOutputTokens = Math.Max(runtimeConfig.MaxTokens, 256);

        var lessonCallEstimate = EstimateExpectedChargeTokenAmount(
            inputTokens,
            lessonOutputTokens,
            runtimeConfig.InputCostPer1M,
            runtimeConfig.OutputCostPer1M);

        var quizCallEstimate = EstimateExpectedChargeTokenAmount(
            inputTokens,
            quizOutputTokens,
            runtimeConfig.InputCostPer1M,
            runtimeConfig.OutputCostPer1M);

        var baseEstimate = (lessonCallEstimate * pendingLessonCount) + (quizCallEstimate * pendingQuizCount);
        return Math.Ceiling(baseEstimate * UpfrontEstimateSafetyMultiplier);
    }

    private static decimal EstimateExpectedChargeTokenAmount(
        int inputTokens,
        int outputTokens,
        decimal inputCostPer1M,
        decimal outputCostPer1M)
    {
        const decimal oneMillion = 1_000_000m;
        var rawCharge = ((decimal)inputTokens / oneMillion) * inputCostPer1M
                        + ((decimal)outputTokens / oneMillion) * outputCostPer1M;

        var chargedTokens = Math.Ceiling(rawCharge);
        if (chargedTokens <= 0m && (inputTokens > 0 || outputTokens > 0))
        {
            chargedTokens = 1m;
        }

        return chargedTokens;
    }

    private static RuntimeConfig ParseRuntimeConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return RuntimeConfig.Default;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString
            };

            var parsed = JsonSerializer.Deserialize<RuntimeConfig>(configJson, options);
            if (parsed == null)
            {
                return RuntimeConfig.Default;
            }

            if (parsed.MaxTokens <= 0)
            {
                parsed.MaxTokens = RuntimeConfig.Default.MaxTokens;
            }

            return parsed;
        }
        catch
        {
            return RuntimeConfig.Default;
        }
    }

    private static bool IsPrivilegedRole(string? roleName)
        => string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);

    private static bool IsMentorRole(string? roleName)
        => string.Equals(roleName, "Mentor", StringComparison.OrdinalIgnoreCase);

    private sealed class RuntimeConfig
    {
        public int MaxTokens { get; set; } = 8192;
        public decimal InputCostPer1M { get; set; } = 0m;
        public decimal OutputCostPer1M { get; set; } = 0m;

        public static RuntimeConfig Default => new();
    }
}
