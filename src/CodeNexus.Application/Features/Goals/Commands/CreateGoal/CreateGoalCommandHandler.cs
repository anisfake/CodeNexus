using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Goals.Commands.CreateGoal;

public class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, Result<CreateGoalResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IGoalValidationService _goalValidationService;
    private const decimal MinGoalMappingConfidence = 0.75m;

    public CreateGoalCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IGoalValidationService goalValidationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _goalValidationService = goalValidationService;
    }

    public async Task<Result<CreateGoalResponseDto>> Handle(CreateGoalCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId && !s.IsDeleted, cancellationToken);

        if (subject == null)
        {
            return Result<CreateGoalResponseDto>.Failure(
                "SUBJECT_NOT_FOUND",
                "Subject not found.");
        }

        var isValid = await _goalValidationService.IsRelatedToProgrammingAsync(request.Title, cancellationToken);
        if (!isValid)
        {
            return Result<CreateGoalResponseDto>.Failure(
                "INVALID_GOAL",
                "Goal must be related to programming or software development.");
        }

        var isRelevantToSubject = await _goalValidationService.IsGoalRelevantToSubjectAsync(
            request.Title,
            request.Description,
            subject.Name,
            subject.Description,
            cancellationToken);

        if (!isRelevantToSubject)
        {
            return Result<CreateGoalResponseDto>.Failure(
                "GOAL_SUBJECT_MISMATCH",
                "Goal is not relevant to the selected subject.");
        }

        var existingGoal = await _context.Goals
            .FirstOrDefaultAsync(g =>
                g.CreatedByUserId == userId
                && g.Title.ToLower() == request.Title.ToLower()
                && !g.IsDeleted,
                cancellationToken);

        if (existingGoal != null)
        {
            return Result<CreateGoalResponseDto>.Failure(
                "GOAL_ALREADY_EXISTS",
                "You already have this goal");
        }

        var goal = new Domain.Entities.Goals
        {
            GoalId = NewId.NextGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Duration = request.Duration,
            IsSystemDefined = false,
            CreatedByUserId = userId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _context.Goals.AddAsync(goal, cancellationToken);
            await _context.SubjectGoals.AddAsync(new SubjectGoal
            {
                SubjectId = subject.SubjectId,
                GoalId = goal.GoalId
            }, cancellationToken);

            var systemGoals = await _context.Goals
                .Where(g => g.IsSystemDefined && g.IsActive && !g.IsDeleted)
                .Join(
                    _context.SubjectGoals.Where(sg => sg.SubjectId == subject.SubjectId),
                    g => g.GoalId,
                    sg => sg.GoalId,
                    (g, sg) => g)
                .ToListAsync(cancellationToken);

            if (systemGoals.Count > 0)
            {
                var candidates = systemGoals
                    .Select(g => new GoalMatchCandidate(g.GoalId, g.Title, g.Description))
                    .ToList();

                var matchResult = await _goalValidationService.FindBestSystemGoalMatchAsync(
                    goal.Title,
                    goal.Description,
                    subject.Name,
                    subject.Description,
                    candidates,
                    cancellationToken);

                if (matchResult.GoalId.HasValue
                    && matchResult.Confidence.HasValue
                    && matchResult.Confidence.Value >= MinGoalMappingConfidence)
                {
                    await _context.GoalMappings.AddAsync(new GoalMapping
                    {
                        MappingId = NewId.NextGuid(),
                        UserGoalId = goal.GoalId,
                        SystemGoalId = matchResult.GoalId.Value,
                        Confidence = matchResult.Confidence.Value,
                        VerifiedByAI = true,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<CreateGoalResponseDto>.Failure(
                "CREATE_GOAL_FAILED",
                $"An error occurred while creating the goal: {ex.Message}");
        }

        var responseDto = new CreateGoalResponseDto(
            goal.GoalId,
            goal.Title,
            goal.Description,
            goal.IsSystemDefined,
            goal.Duration,
            goal.DurationInDays
        );

        return Result<CreateGoalResponseDto>.Success(responseDto);
    }
}
