using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateGoalSupplementLearningPath;

public class GenerateGoalSupplementLearningPathCommandHandler
    : IRequestHandler<GenerateGoalSupplementLearningPathCommand, Result<GoalSupplementLearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISender _sender;

    public GenerateGoalSupplementLearningPathCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISender sender)
    {
        _context = context;
        _currentUserService = currentUserService;
        _sender = sender;
    }

    public async Task<Result<GoalSupplementLearningPathResponse>> Handle(
        GenerateGoalSupplementLearningPathCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var sourcePath = await _context.LearningPaths
            .AsNoTracking()
            .Where(x => x.PathId == request.SourcePathId)
            .Select(x => new SourcePathInfo(
                x.PathId,
                x.UserId,
                x.SubjectId,
                x.Title,
                x.ComplexityLevel,
                x.Language))
            .FirstOrDefaultAsync(cancellationToken);

        if (sourcePath == null)
        {
            return Result<GoalSupplementLearningPathResponse>.Failure(
                "LEARNING_PATH_NOT_FOUND",
                "Learning path not found.");
        }

        if (sourcePath.UserId != userId)
        {
            return Result<GoalSupplementLearningPathResponse>.Failure(
                "ACCESS_DENIED",
                "You do not have permission to create a supplement path for this learning path.");
        }

        var sourceGoal = await _context.LearningPathGoals
            .AsNoTracking()
            .Where(x => x.PathId == request.SourcePathId && x.GoalId == request.GoalId)
            .Join(
                _context.Goals.AsNoTracking(),
                learningPathGoal => learningPathGoal.GoalId,
                goal => goal.GoalId,
                (learningPathGoal, goal) => new SourceGoalInfo(
                    learningPathGoal.GoalId,
                    goal.Title,
                    goal.Description,
                    learningPathGoal.Weight))
            .FirstOrDefaultAsync(cancellationToken);

        if (sourceGoal == null)
        {
            return Result<GoalSupplementLearningPathResponse>.Failure(
                "GOAL_NOT_FOUND_IN_PATH",
                "Goal is not part of the source learning path.");
        }

        await TrySyncSourceGoalProgressAsync(request.SourcePathId, userId, cancellationToken);

        var sourcePathProgress = await _context.UserGoalProgresses
            .AsNoTracking()
            .Where(x =>
                x.UserId == userId &&
                x.GoalId == request.GoalId &&
                x.LearningPathId == request.SourcePathId)
            .Select(x => x.ProgressPercent)
            .FirstOrDefaultAsync(cancellationToken);

        var aggregateProgress = await _context.UserGoalProgresses
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.GoalId == request.GoalId)
            .SumAsync(x => x.ProgressPercent, cancellationToken);

        var currentProgress = Math.Round(Math.Clamp(aggregateProgress, 0m, 100m), 2);
        var remainingPercent = Math.Round(100m - currentProgress, 2);

        if (remainingPercent <= 0m)
        {
            return Result<GoalSupplementLearningPathResponse>.Failure(
                "GOAL_ALREADY_COMPLETED",
                "This goal is already completed.");
        }

        if (sourcePathProgress <= 0m)
        {
            return Result<GoalSupplementLearningPathResponse>.Failure(
                "GOAL_PROGRESS_NOT_FOUND",
                "The source learning path has not contributed progress to this goal yet.");
        }

        var contributionWeight = Math.Round(remainingPercent / 100m, 4);
        var generationCommand = new GenerateLearningPathSkeletonCommand(
            sourcePath.SubjectId,
            new List<LearningPathGoalRequest> { new(request.GoalId, contributionWeight) },
            request.ComplexityLevel ?? sourcePath.ComplexityLevel,
            request.LanguageSelection ?? sourcePath.Language,
            request.SaveAsDraft,
            UseAbsoluteGoalWeights: true,
            GenerationContextInstruction: BuildSupplementInstruction(
                sourcePath.Title,
                sourceGoal.Title,
                currentProgress,
                remainingPercent));

        var generationResult = await _sender.Send(generationCommand, cancellationToken);
        if (!generationResult.IsSuccess)
        {
            return Result<GoalSupplementLearningPathResponse>.Failure(
                generationResult.ErrorCode ?? "SUPPLEMENT_GENERATION_FAILED",
                generationResult.ErrorMessage ?? "Failed to generate supplement learning path.");
        }

        return Result<GoalSupplementLearningPathResponse>.Success(
            new GoalSupplementLearningPathResponse(
                request.SourcePathId,
                request.GoalId,
                currentProgress,
                remainingPercent,
                generationResult.Value!));
    }

    private async Task TrySyncSourceGoalProgressAsync(
        Guid sourcePathId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var changed = await UserGoalProgressSyncHelper.SyncForLearningPathAsync(
                _context,
                sourcePathId,
                userId,
                cancellationToken);

            if (changed)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // If sync cannot run in the current context, use the persisted progress rows.
        }
    }

    private static string BuildSupplementInstruction(
        string sourcePathTitle,
        string goalTitle,
        decimal currentProgress,
        decimal remainingPercent)
        => $"""
This is a supplemental learning path, not a full restart.
The learner has already completed about {currentProgress:0.##}% of the goal "{goalTitle}" through the source path "{sourcePathTitle}".
Generate content that targets the remaining {remainingPercent:0.##}% only.
Avoid repeating broad foundations already covered by the source path; focus on missing depth, advanced application, practice, and integration needed to complete the goal.
The learning path goal weight is intentionally below 100% and represents only the missing contribution.
""";

    private sealed record SourcePathInfo(
        Guid PathId,
        Guid UserId,
        Guid SubjectId,
        string Title,
        Domain.Enums.ComplexityLevel ComplexityLevel,
        Domain.Enums.LanguageSelection Language);

    private sealed record SourceGoalInfo(
        Guid GoalId,
        string Title,
        string? Description,
        decimal Weight);
}
