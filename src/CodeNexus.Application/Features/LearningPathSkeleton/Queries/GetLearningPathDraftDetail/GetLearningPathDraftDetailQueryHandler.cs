using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathDraftDetail;

public class GetLearningPathDraftDetailQueryHandler : IRequestHandler<GetLearningPathDraftDetailQuery, Result<LearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLearningPathDraftDetailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathResponse>> Handle(GetLearningPathDraftDetailQuery request, CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathResponse>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var mentor = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == mentorId, cancellationToken);

        if (mentor == null)
        {
            return Result<LearningPathResponse>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathResponse>.Failure("ACCESS_DENIED", "Only mentors can view draft learning path details.");
        }

        var learningPath = await _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.Subject)
            .Include(lp => lp.LearningPathGoals)
                .ThenInclude(lpg => lpg.Goal)
            .Include(lp => lp.User)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks)
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (learningPath == null)
        {
            return Result<LearningPathResponse>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (learningPath.UserId != mentorId)
        {
            return Result<LearningPathResponse>.Failure("ACCESS_DENIED", "You can only view your own draft learning paths.");
        }

        if (!string.Equals(learningPath.Status, LearningPathStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathResponse>.Failure("INVALID_STATUS", "Learning path is not in draft status.");
        }

        var response = new LearningPathResponse(
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
                    null
                )).ToList(),
            learningPath.StartDate,
            learningPath.EndDate,
            learningPath.Title,
            learningPath.Description,
            learningPath.Status,
            learningPath.CreatedByType,
            learningPath.UserId,
            learningPath.User.Username,
            learningPath.Chapters.Select(c => new ChapterDto(
                c.ChapterId,
                c.Title,
                c.Content,
                c.OrderIndex,
                c.Lessons.Select(l => new LessonDto(
                    l.LessonId,
                    l.Title,
                    l.Content,
                    l.LessonDay,
                    l.Quizzes.Select(q => new QuizDto(
                        q.QuizId,
                        q.Title,
                        q.Description
                    )).ToList()
                )).ToList(),
                c.Tasks.Select(t => new TaskDto(
                    t.TaskId,
                    t.Title,
                    t.Description ?? string.Empty,
                    t.TaskType,
                    t.Priority,
                    t.Status,
                    t.DueDate,
                    t.QuizQuestionsJson
                )).ToList()
            )).ToList(),
            learningPath.Chapters.Count(c => !c.IsDeleted),
            learningPath.CreatedAt
        );

        return Result<LearningPathResponse>.Success(response);
    }
}
