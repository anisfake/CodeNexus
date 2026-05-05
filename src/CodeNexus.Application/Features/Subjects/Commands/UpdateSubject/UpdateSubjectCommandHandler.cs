using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subjects.Commands.UpdateSubject;

public class UpdateSubjectCommandHandler : IRequestHandler<UpdateSubjectCommand, Result<SubjectDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateSubjectCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SubjectDto>> Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var user = await _context.Users
            .Where(u => u.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);

        var subject = await _context.Subjects
            .Include(s => s.SubjectGoals)
                .ThenInclude(sg => sg.Goal)
            .FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId, cancellationToken);

        if (subject == null)
        {
            return Result<SubjectDto>.Failure("SUBJECT_NOT_FOUND", "Subject not found.");
        }

        if (subject.CreatedByUserId != userId)
        {
            return Result<SubjectDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var duplicateSubject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.Name.ToLower() == request.Name.ToLower()
                && s.SubjectId != request.SubjectId, cancellationToken);

        if (duplicateSubject != null)
        {
            return Result<SubjectDto>.Failure("SUBJECT_EXISTS", "A subject with this name already exists.");
        }

        subject.Name = request.Name;
        subject.Description = request.Description;
        subject.Color = request.Color;
        subject.Icon = request.Icon;
        subject.Category = request.Category;

        var goalDtos = new List<SubjectGoalDto>();
        if (request.Goals != null)
        {
            var incomingGoalIds = request.Goals
                .Where(g => g.GoalId.HasValue)
                .Select(g => g.GoalId!.Value)
                .ToHashSet();

            var toRemove = subject.SubjectGoals
                .Where(sg => !incomingGoalIds.Contains(sg.GoalId))
                .ToList();
            foreach (var sg in toRemove)
                subject.SubjectGoals.Remove(sg);

            foreach (var goalRequest in request.Goals)
            {
                if (goalRequest.GoalId.HasValue)
                {
                    var existingGoal = subject.SubjectGoals
                        .FirstOrDefault(sg => sg.GoalId == goalRequest.GoalId.Value)?.Goal;

                    if (existingGoal != null && existingGoal.IsSystemDefined)
                    {
                        existingGoal.Title = goalRequest.Title;
                        existingGoal.Description = goalRequest.Description;
                        existingGoal.Duration = goalRequest.Duration;
                        existingGoal.UpdatedAt = DateTime.UtcNow;
                        goalDtos.Add(new SubjectGoalDto(existingGoal.GoalId, existingGoal.Title, existingGoal.Description, existingGoal.IsSystemDefined, existingGoal.DurationInDays));
                    }
                }
                else
                {
                    var newGoal = new CodeNexus.Domain.Entities.Goals
                    {
                        GoalId = NewId.NextGuid(),
                        Title = goalRequest.Title,
                        Description = goalRequest.Description,
                        Duration = goalRequest.Duration,
                        IsSystemDefined = true,
                        CreatedByUserId = null,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Goals.AddAsync(newGoal, cancellationToken);
                    subject.SubjectGoals.Add(new SubjectGoal
                    {
                        SubjectId = subject.SubjectId,
                        GoalId = newGoal.GoalId
                    });

                    goalDtos.Add(new SubjectGoalDto(newGoal.GoalId, newGoal.Title, newGoal.Description, newGoal.IsSystemDefined, newGoal.DurationInDays));
                }
            }
        }
        else
        {
            goalDtos = subject.SubjectGoals
                .Where(sg => !sg.Goal.IsDeleted)
                .Select(sg => new SubjectGoalDto(sg.GoalId, sg.Goal.Title, sg.Goal.Description, sg.Goal.IsSystemDefined, sg.Goal.DurationInDays))
                .ToList();
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<SubjectDto>.Failure("UPDATE_SUBJECT_FAILED", $"An error occurred while updating the subject: {ex.Message}");
        }

        var subjectDto = new SubjectDto(
            subject.SubjectId,
            subject.Name,
            subject.Description,
            subject.Color,
            subject.Icon,
            subject.Category,
            goalDtos,
            user.FirstName + " " + user.LastName,
            subject.CreatedByUserId,
            subject.CreatedAt
        );

        return Result<SubjectDto>.Success(subjectDto);
    }
}
