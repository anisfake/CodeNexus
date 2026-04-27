using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subjects.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subjects.Commands.CreateSubject;

public class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, Result<SubjectDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateSubjectCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SubjectDto>> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user == null || user.Role?.RoleName != "Mentor")
        {
            return Result<SubjectDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var existingSubject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.Name.ToLower() == request.Name.ToLower(), cancellationToken);

        if (existingSubject != null)
        {
            return Result<SubjectDto>.Failure("SUBJECT_EXISTS", "A subject with this name already exists.");
        }

        var subject = new Subject
        {
            SubjectId = NewId.NextGuid(),
            Name = request.Name,
            Description = request.Description,
            Color = request.Color,
            Icon = request.Icon,
            Category = request.Category,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        var goalDtos = new List<SubjectGoalDto>();
        if (request.Goals != null && request.Goals.Count > 0)
        {
            foreach (var goalRequest in request.Goals)
            {
                var goal = new CodeNexus.Domain.Entities.Goals
                {
                    GoalId = NewId.NextGuid(),
                    Title = goalRequest.Title,
                    Description = goalRequest.Description,
                    Duration = goalRequest.Duration,
                    IsSystemDefined = true,
                    CreatedByUserId = null,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Goals.AddAsync(goal, cancellationToken);

                subject.SubjectGoals.Add(new SubjectGoal
                {
                    SubjectId = subject.SubjectId,
                    GoalId = goal.GoalId
                });

                goalDtos.Add(new SubjectGoalDto(goal.GoalId, goal.Title, goal.Description, goal.IsSystemDefined, goal.DurationInDays));
            }
        }

        try
        {
            await _context.Subjects.AddAsync(subject, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<SubjectDto>.Failure("ADD_SUBJECT_FAILED", $"An error occurred while adding the subject: {ex.Message}");
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
