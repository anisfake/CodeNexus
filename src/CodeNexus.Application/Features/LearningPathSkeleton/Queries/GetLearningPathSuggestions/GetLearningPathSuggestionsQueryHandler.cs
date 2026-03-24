using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSuggestions;

public class GetLearningPathSuggestionsQueryHandler : IRequestHandler<GetLearningPathSuggestionsQuery, Result<List<LearningPathSuggestionDto>>>
{
    private const decimal ScoreThreshold = 0.8m;
    private const decimal MaxWeightDiff = 1m;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGoalValidationService _goalValidationService;

    public GetLearningPathSuggestionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IGoalValidationService goalValidationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _goalValidationService = goalValidationService;
    }

    public async Task<Result<List<LearningPathSuggestionDto>>> Handle(GetLearningPathSuggestionsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var subject = await _context.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubjectId == request.SubjectId, cancellationToken);

        if (subject == null)
        {
            return Result<List<LearningPathSuggestionDto>>.Failure("SUBJECT_NOT_FOUND", "Subject not found");
        }

        if (request.Goals == null || request.Goals.Count == 0)
        {
            return Result<List<LearningPathSuggestionDto>>.Failure("GOALS_REQUIRED", "At least one goal is required");
        }

        if (request.Goals.Count > 2)
        {
            return Result<List<LearningPathSuggestionDto>>.Failure("GOALS_LIMIT_EXCEEDED", "You can select up to 2 goals only");
        }

        var uniqueGoalIds = request.Goals.Select(g => g.GoalId).Distinct().ToList();
        if (uniqueGoalIds.Count != request.Goals.Count)
        {
            return Result<List<LearningPathSuggestionDto>>.Failure("DUPLICATE_GOALS", "Duplicate goals are not allowed");
        }

        var goals = await _context.Goals
            .AsNoTracking()
            .Where(g => uniqueGoalIds.Contains(g.GoalId) && !g.IsDeleted && g.IsActive)
            .ToListAsync(cancellationToken);

        if (goals.Count != uniqueGoalIds.Count)
        {
            return Result<List<LearningPathSuggestionDto>>.Failure("GOAL_NOT_FOUND", "One or more goals were not found");
        }

        var invalidUserGoals = goals
            .Where(g => !g.IsSystemDefined && g.CreatedByUserId != userId)
            .ToList();

        if (invalidUserGoals.Count > 0)
        {
            return Result<List<LearningPathSuggestionDto>>.Failure("GOAL_NOT_FOUND", "One or more goals were not found");
        }

        var systemGoalIds = goals
            .Where(g => g.IsSystemDefined)
            .Select(g => g.GoalId)
            .ToList();

        if (systemGoalIds.Count > 0)
        {
            var mappedSystemGoalIds = await _context.SubjectGoals
                .Where(sg => sg.SubjectId == request.SubjectId && systemGoalIds.Contains(sg.GoalId))
                .Select(sg => sg.GoalId)
                .ToListAsync(cancellationToken);

            if (mappedSystemGoalIds.Count != systemGoalIds.Count)
            {
                return Result<List<LearningPathSuggestionDto>>.Failure(
                    "GOAL_SUBJECT_MISMATCH",
                    "One or more system goals are not available for the selected subject");
            }
        }

        var normalizedGoals = NormalizeGoalWeights(request.Goals);

        var goalMappings = await _context.GoalMappings
            .AsNoTracking()
            .Where(m => uniqueGoalIds.Contains(m.UserGoalId))
            .ToListAsync(cancellationToken);

        var mappingByUserGoalId = goalMappings
            .GroupBy(m => m.UserGoalId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Confidence).First());

        var userGoals = normalizedGoals
            .Join(goals, ng => ng.GoalId, g => g.GoalId, (ng, g) => new UserGoalInfo(
                g.GoalId,
                g.Title,
                g.Description,
                g.IsSystemDefined,
                ng.Weight,
                mappingByUserGoalId.TryGetValue(g.GoalId, out var map) ? map.SystemGoalId : (Guid?)null
            ))
            .ToList();

        var candidates = await _context.LearningPaths
            .AsNoTracking()
            .Where(lp =>
                lp.SubjectId == request.SubjectId
                && lp.UserId != userId
                && lp.Language == request.LanguageSelection
                && lp.ComplexityLevel == request.ComplexityLevel)
            .Select(lp => new CandidatePath(lp.PathId, lp.Title, lp.Description, lp.CreatedAt))
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return Result<List<LearningPathSuggestionDto>>.Success(new List<LearningPathSuggestionDto>());
        }

        var candidateIds = candidates.Select(c => c.PathId).ToList();

        var candidateGoals = await _context.LearningPathGoals
            .AsNoTracking()
            .Where(lpg => candidateIds.Contains(lpg.PathId))
            .Join(_context.Goals,
                lpg => lpg.GoalId,
                g => g.GoalId,
                (lpg, g) => new CandidateGoal(
                    lpg.PathId,
                    lpg.GoalId,
                    lpg.Weight,
                    g.Title,
                    g.Description,
                    g.DurationInDays))
            .ToListAsync(cancellationToken);

        var goalsByPathId = candidateGoals
            .GroupBy(g => g.PathId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var suggestions = new List<LearningPathSuggestionDto>();

        foreach (var candidate in candidates)
        {
            goalsByPathId.TryGetValue(candidate.PathId, out var pathGoals);
            pathGoals ??= new List<CandidateGoal>();

            var score = await CalculateScoreAsync(userGoals, pathGoals, subject.Name, subject.Description, cancellationToken);

            if (score >= ScoreThreshold)
            {
                var goalDtos = pathGoals
                    .Select(g => new LearningPathGoalDto(g.GoalId, g.Title, g.Weight, g.DurationInDays))
                    .ToList();

                suggestions.Add(new LearningPathSuggestionDto(
                    candidate.PathId,
                    candidate.Title,
                    candidate.Description ?? string.Empty,
                    score,
                    goalDtos,
                    null
                ));
            }
        }

        return Result<List<LearningPathSuggestionDto>>.Success(
            suggestions
                .OrderByDescending(s => s.Score)
                .Take(5)
                .ToList());
    }

    private async Task<decimal> CalculateScoreAsync(
        List<UserGoalInfo> userGoals,
        List<CandidateGoal> candidateGoals,
        string subjectName,
        string? subjectDescription,
        CancellationToken cancellationToken)
    {
        decimal total = 0m;

        foreach (var userGoal in userGoals)
        {
            var match = await GetBestMatchAsync(userGoal, candidateGoals, subjectName, subjectDescription, cancellationToken);
            total += userGoal.Weight * match.WeightAlignment * match.Similarity;
        }

        return total;
    }

    private async Task<MatchResult> GetBestMatchAsync(
        UserGoalInfo userGoal,
        List<CandidateGoal> candidateGoals,
        string subjectName,
        string? subjectDescription,
        CancellationToken cancellationToken)
    {
        if (candidateGoals.Count == 0)
        {
            return new MatchResult(0m, 0m);
        }

        if (userGoal.SystemGoalId.HasValue || userGoal.IsSystemDefined)
        {
            var targetGoalId = userGoal.SystemGoalId ?? userGoal.GoalId;
            var matched = candidateGoals.FirstOrDefault(g => g.GoalId == targetGoalId);

            if (matched == null)
            {
                return new MatchResult(0m, 0m);
            }

            var weightAlignment = CalculateWeightAlignment(userGoal.Weight, matched.Weight);
            return new MatchResult(weightAlignment, 1m);
        }

        var candidates = candidateGoals
            .Select(g => new GoalMatchCandidate(g.GoalId, g.Title, g.Description))
            .ToList();

        var matchResult = await _goalValidationService.FindBestSystemGoalMatchAsync(
            userGoal.Title,
            userGoal.Description,
            subjectName,
            subjectDescription,
            candidates,
            cancellationToken);

        if (!matchResult.GoalId.HasValue || !matchResult.Confidence.HasValue)
        {
            return new MatchResult(0m, 0m);
        }

        var matchedGoal = candidateGoals.FirstOrDefault(g => g.GoalId == matchResult.GoalId.Value);
        if (matchedGoal == null)
        {
            return new MatchResult(0m, 0m);
        }

        var alignment = CalculateWeightAlignment(userGoal.Weight, matchedGoal.Weight);
        return new MatchResult(alignment, Clamp01(matchResult.Confidence.Value));
    }

    private static decimal CalculateWeightAlignment(decimal userWeight, decimal candidateWeight)
    {
        var diff = Math.Abs(userWeight - candidateWeight);
        var normalized = diff > MaxWeightDiff ? 1m : diff / MaxWeightDiff;
        return Clamp01(1m - normalized);
    }

    private static decimal Clamp01(decimal value)
    {
        if (value < 0m) return 0m;
        if (value > 1m) return 1m;
        return value;
    }

    private sealed record CandidatePath(Guid PathId, string Title, string? Description, DateTime CreatedAt);
    private sealed record CandidateGoal(Guid PathId, Guid GoalId, decimal Weight, string Title, string? Description, int DurationInDays);
    private sealed record UserGoalInfo(Guid GoalId, string Title, string? Description, bool IsSystemDefined, decimal Weight, Guid? SystemGoalId);
    private sealed record MatchResult(decimal WeightAlignment, decimal Similarity);

    private sealed record NormalizedGoal(Guid GoalId, decimal Weight);

    private static List<NormalizedGoal> NormalizeGoalWeights(List<LearningPathGoalRequest> goals)
    {
        var usePercent = goals.Any(g => g.Weight > 1m);
        var scaled = goals.Select(g => new NormalizedGoal(
            g.GoalId,
            usePercent ? g.Weight / 100m : g.Weight
        )).ToList();

        var sum = scaled.Sum(g => g.Weight);
        if (sum <= 0)
        {
            throw new InvalidOperationException("Goal weights must be greater than 0");
        }

        return scaled.Select(g => new NormalizedGoal(g.GoalId, g.Weight / sum)).ToList();
    }
}
