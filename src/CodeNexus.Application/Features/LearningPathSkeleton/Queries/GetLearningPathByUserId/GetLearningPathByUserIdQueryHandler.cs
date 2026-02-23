using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathByUserId;

public class GetLearningPathByUserIdQueryHandler : IRequestHandler<GetLearningPathByUserIdQuery, Result<PaginationDto<LearningPathResponse>>>
{
    private readonly IApplicationDbContext _context;

    public GetLearningPathByUserIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginationDto<LearningPathResponse>>> Handle(GetLearningPathByUserIdQuery request, CancellationToken cancellationToken)
    {
        var userExists = await _context.Users.AnyAsync(u => u.UserId == request.UserId, cancellationToken);

        if (!userExists)
        {
            return Result<PaginationDto<LearningPathResponse>>.Failure("USER_NOT_FOUND", "User not found.");
        }

        var query = _context.LearningPaths
            .Include(lp => lp.Subject)
            .Include(lp => lp.Goal)
            .Include(lp => lp.User)
            .Include(lp => lp.Chapters).ThenInclude(c => c.Lessons).ThenInclude(l => l.Quizzes)
            .Include(lp => lp.Chapters).ThenInclude(c => c.Tasks)
            .Where(lp => lp.UserId == request.UserId)
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

        if (request.Status.HasValue)
        {
            query = query.Where(lp => lp.Status == request.Status.Value.ToString());
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
                lp.Goal.GoalId,
                lp.Goal.Title,
                lp.StartDate,
                lp.EndDate,
                lp.Title,
                lp.Description,
                lp.Status.ToString(),
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
                        l.Quizzes.Select(q => new QuizDto(
                            q.QuizId,
                            q.Title,
                            q.Description
                        )).ToList()
                    )).ToList(),
                    c.Tasks.Select(t => new TaskDto(
                        t.TaskId,
                        t.Title,
                        t.Description
                    )).ToList()
                )).ToList(),
                lp.Chapters.Count(),
                lp.CreatedAt
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
