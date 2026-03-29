using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetLearningPathSharePreview;

public class GetLearningPathSharePreviewQueryHandler : IRequestHandler<GetLearningPathSharePreviewQuery, Result<LearningPathSharePreviewDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLearningPathSharePreviewQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathSharePreviewDto>> Handle(GetLearningPathSharePreviewQuery request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathSharePreviewDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == studentId, cancellationToken);

        if (student == null)
        {
            return Result<LearningPathSharePreviewDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathSharePreviewDto>.Failure("ACCESS_DENIED", "Only students can preview shared learning paths.");
        }

        var share = await _context.LearningPathShares
            .AsNoTracking()
            .Include(s => s.Mentor)
            .Include(s => s.Student)
            .FirstOrDefaultAsync(s => s.ShareId == request.ShareId && s.StudentId == studentId, cancellationToken);

        if (share == null)
        {
            return Result<LearningPathSharePreviewDto>.Failure("SHARE_NOT_FOUND", "Learning path share not found.");
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
            .FirstOrDefaultAsync(lp => lp.PathId == share.PathId, cancellationToken);

        if (learningPath == null)
        {
            return Result<LearningPathSharePreviewDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
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

        return Result<LearningPathSharePreviewDto>.Success(new LearningPathSharePreviewDto(
            share.ShareId,
            share.MentorId,
            share.Mentor.Username,
            share.StudentId,
            share.Student.Username,
            share.Status,
            share.SentAt,
            share.RespondedAt,
            learningPathResponse
        ));
    }
}
