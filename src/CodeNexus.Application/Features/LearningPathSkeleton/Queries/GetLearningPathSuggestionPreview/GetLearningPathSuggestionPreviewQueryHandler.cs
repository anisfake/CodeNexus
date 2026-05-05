using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSuggestionPreview;

public class GetLearningPathSuggestionPreviewQueryHandler
    : IRequestHandler<GetLearningPathSuggestionPreviewQuery, Result<LearningPathSuggestionPreviewDto>>
{
    private const decimal ScoreThreshold = 0.8m;
    private const decimal MaxWeightDiff = 1m;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGoalValidationService _goalValidationService;

    public GetLearningPathSuggestionPreviewQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IGoalValidationService goalValidationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _goalValidationService = goalValidationService;
    }

    public async Task<Result<LearningPathSuggestionPreviewDto>> Handle(
        GetLearningPathSuggestionPreviewQuery request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var subject = await _context.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubjectId == request.SubjectId, cancellationToken);

        if (subject == null)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure("SUBJECT_NOT_FOUND", "Subject not found.");
        }

        if (request.Goals == null || request.Goals.Count == 0)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure("GOALS_REQUIRED", "At least one goal is required");
        }

        if (request.Goals.Count > 2)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure("GOALS_LIMIT_EXCEEDED", "You can select up to 2 goals only");
        }

        var uniqueGoalIds = request.Goals.Select(g => g.GoalId).Distinct().ToList();
        if (uniqueGoalIds.Count != request.Goals.Count)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure("DUPLICATE_GOALS", "Duplicate goals are not allowed");
        }

        var goals = await _context.Goals
            .AsNoTracking()
            .Where(g => uniqueGoalIds.Contains(g.GoalId) && !g.IsDeleted)
            .ToListAsync(cancellationToken);

        if (goals.Count != uniqueGoalIds.Count)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure("GOAL_NOT_FOUND", "Goal not found.");
        }

        var invalidUserGoals = goals
            .Where(g => !g.IsSystemDefined && g.CreatedByUserId != userId)
            .ToList();

        if (invalidUserGoals.Count > 0)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure("GOAL_NOT_FOUND", "Goal not found.");
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
                return Result<LearningPathSuggestionPreviewDto>.Failure(
                    "GOAL_SUBJECT_MISMATCH",
                    "Goal is not relevant to the selected subject.");
            }
        }

        var candidatePath = await _context.LearningPaths
            .AsNoTracking()
            .FirstOrDefaultAsync(lp => lp.PathId == request.SuggestedPathId, cancellationToken);

        if (candidatePath == null)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (candidatePath.UserId == userId)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure(
                "CANNOT_PREVIEW_OWN_PATH",
                "You cannot preview your own learning path in suggestions.");
        }

        if (candidatePath.SubjectId != request.SubjectId
            || candidatePath.Language != request.LanguageSelection
            || candidatePath.ComplexityLevel != request.ComplexityLevel)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure(
                "SUGGESTION_CONTEXT_MISMATCH",
                "Suggested learning path does not match the selected subject, complexity, or language.");
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

        var candidateGoals = await _context.LearningPathGoals
            .AsNoTracking()
            .Where(lpg => lpg.PathId == candidatePath.PathId)
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

        var score = await CalculateScoreAsync(
            userGoals,
            candidateGoals,
            subject.Name,
            subject.Description,
            cancellationToken);

        if (score < ScoreThreshold)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure(
                "SUGGESTION_NOT_ELIGIBLE",
                "This learning path is no longer equivalent to your selected goals.");
        }

        var learningPath = await _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.Subject)
            .Include(lp => lp.User)
            .Include(lp => lp.LearningPathGoals)
                .ThenInclude(lpg => lpg.Goal)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(lp => lp.PathId == request.SuggestedPathId, cancellationToken);

        if (learningPath == null)
        {
            return Result<LearningPathSuggestionPreviewDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        var learningPathResponse = new LearningPathResponse(
            learningPath.PathId,
            learningPath.SubjectId,
            learningPath.Subject.Name,
            learningPath.LearningPathGoals
                .OrderByDescending(g => g.Weight)
                .Select(g => new LearningPathGoalDto(
                    g.GoalId,
                    g.Goal.Title,
                    g.Weight,
                    g.Goal.DurationInDays,
                    "NotStarted",
                    null,
                    0m,
                    g.Weight * 100m
                )).ToList(),
            learningPath.StartDate,
            learningPath.EndDate,
            learningPath.Title,
            learningPath.Description ?? string.Empty,
            learningPath.Status,
            learningPath.CreatedByType,
            learningPath.UserId,
            learningPath.User.Username,
            learningPath.Chapters
                .OrderBy(c => c.OrderIndex)
                .Select(c => new ChapterDto(
                    c.ChapterId,
                    c.Title,
                    c.Content,
                    c.OrderIndex,
                    c.Lessons
                        .OrderBy(l => l.OrderIndex)
                        .Select(l => new LessonDto(
                            l.LessonId,
                            l.Title,
                            l.Content,
                            l.LessonDay,
                            l.Quizzes
                                .OrderBy(q => q.CreatedAt)
                                .Select(q => new QuizDto(
                                    q.QuizId,
                                    q.Title,
                                    q.Description ?? string.Empty
                                ))
                                .ToList()
                        ))
                        .ToList(),
                    c.Tasks
                        .OrderBy(t => t.CreatedAt)
                        .Select(t => new TaskDto(
                            t.TaskId,
                            t.Title,
                            t.Description ?? string.Empty,
                            t.TaskType,
                            t.Priority,
                            t.Status,
                            t.DueDate
                        ))
                        .ToList()
                ))
                .ToList(),
            learningPath.Chapters.Count(c => !c.IsDeleted),
            learningPath.CreatedAt,
            learningPath.ComplexityLevel,
            learningPath.Language
        );

        return Result<LearningPathSuggestionPreviewDto>.Success(new LearningPathSuggestionPreviewDto(
            learningPath.PathId,
            score,
            learningPathResponse));
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

    private sealed record CandidateGoal(
        Guid PathId,
        Guid GoalId,
        decimal Weight,
        string Title,
        string? Description,
        int DurationInDays);

    private sealed record UserGoalInfo(
        Guid GoalId,
        string Title,
        string? Description,
        bool IsSystemDefined,
        decimal Weight,
        Guid? SystemGoalId);

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
