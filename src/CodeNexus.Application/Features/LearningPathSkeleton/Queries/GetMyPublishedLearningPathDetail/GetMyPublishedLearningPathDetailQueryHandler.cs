using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetMyPublishedLearningPathDetail;

public class GetMyPublishedLearningPathDetailQueryHandler : IRequestHandler<GetMyPublishedLearningPathDetailQuery, Result<LearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyPublishedLearningPathDetailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathResponse>> Handle(GetMyPublishedLearningPathDetailQuery request, CancellationToken cancellationToken)
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
            return Result<LearningPathResponse>.Failure("ACCESS_DENIED", "Access denied.");
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
                .ThenInclude(q => q.Questions.Where(qq => !qq.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (learningPath == null)
        {
            return Result<LearningPathResponse>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (learningPath.UserId != mentorId)
        {
            return Result<LearningPathResponse>.Failure("ACCESS_DENIED", "Access denied.");
        }

        if (!string.Equals(learningPath.Status, LearningPathStatus.Published.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathResponse>.Failure("LEARNING_PATH_NOT_PUBLISHED", "Learning path is not published.");
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
                    null,
                    0m,
                    g.Weight * 100m
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
                        q.Description,
                        q.Questions
                            .Where(qq => !qq.IsDeleted)
                            .OrderBy(qq => qq.OrderIndex ?? int.MaxValue)
                            .Select(qq => new QuestionDto(
                                qq.QuestionId,
                                qq.QuestionText,
                                qq.Type ?? QuestionType.SingleChoice,
                                string.IsNullOrWhiteSpace(qq.Options) ? new List<string>() : qq.Options.Split("||").ToList(),
                                qq.CorrectAnswer ?? string.Empty,
                                qq.Points,
                                qq.OrderIndex ?? 0))
                            .ToList()
                    )).ToList()
                )).ToList(),
                c.Tasks
                    .Where(t => !t.IsDeleted)
                    .Select(t => new TaskDto(
                        t.TaskId,
                        t.Title,
                        t.Description ?? string.Empty,
                        t.TaskType,
                        t.Priority,
                        t.Status,
                        t.DueDate
                    )).ToList()
            )).ToList(),
            learningPath.Chapters.Count(c => !c.IsDeleted),
            learningPath.CreatedAt,
            learningPath.ComplexityLevel,
            learningPath.Language
        );

        return Result<LearningPathResponse>.Success(response);
    }
}
