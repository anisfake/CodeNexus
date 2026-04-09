using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetMyLearningPathDrafts;

public class GetMyLearningPathDraftsQueryHandler : IRequestHandler<GetMyLearningPathDraftsQuery, Result<PaginationDto<LearningPathResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyLearningPathDraftsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginationDto<LearningPathResponse>>> Handle(GetMyLearningPathDraftsQuery request, CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<PaginationDto<LearningPathResponse>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var mentor = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == mentorId, cancellationToken);

        if (mentor == null)
        {
            return Result<PaginationDto<LearningPathResponse>>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<PaginationDto<LearningPathResponse>>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var query = _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.Subject)
            .Include(lp => lp.LearningPathGoals)
                .ThenInclude(lpg => lpg.Goal)
            .Include(lp => lp.User)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
            .Where(lp => lp.UserId == mentorId && lp.Status == LearningPathStatus.Draft.ToString())
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(lp =>
                lp.Title.ToLower().Contains(searchTerm) ||
                (lp.Description != null && lp.Description.ToLower().Contains(searchTerm)));
        }

        if (request.SubjectId.HasValue)
        {
            query = query.Where(lp => lp.SubjectId == request.SubjectId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = request.SortDescending
            ? query.OrderByDescending(lp => lp.CreatedAt)
            : query.OrderBy(lp => lp.CreatedAt);

        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(lp => new LearningPathResponse(
                lp.PathId,
                lp.SubjectId,
                lp.Subject.Name,
                lp.LearningPathGoals
                    .OrderByDescending(g => g.Weight)
                    .Select(g => new LearningPathGoalDto(
                        g.GoalId,
                        g.Goal.Title,
                        g.Weight,
                        g.Goal.DurationInDays,
                        "NotStarted",
                        null
                    )).ToList(),
                lp.StartDate,
                lp.EndDate,
                lp.Title,
                lp.Description,
                lp.Status,
                lp.CreatedByType,
                lp.UserId,
                lp.User.Username,
                lp.Chapters.Select(c => new ChapterDto(
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
                            null,
                            "Not Attempted"
                        )).ToList()
                        ,
                        "Not Started"
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
                        t.DueDate,
                        t.QuizQuestionsJson,
                        "Pending"
                    )).ToList()
                )).ToList(),
                lp.Chapters.Count(c => !c.IsDeleted),
                lp.CreatedAt,
                lp.ComplexityLevel,
                lp.Language
            ))
            .ToListAsync(cancellationToken);

        return Result<PaginationDto<LearningPathResponse>>.Success(new PaginationDto<LearningPathResponse>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        });
    }
}
