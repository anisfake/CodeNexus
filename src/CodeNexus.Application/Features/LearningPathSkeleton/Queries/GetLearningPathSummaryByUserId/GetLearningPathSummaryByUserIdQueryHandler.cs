using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetLearningPathSummaryByUserId;

public class GetLearningPathSummaryByUserIdQueryHandler
    : IRequestHandler<GetLearningPathSummaryByUserIdQuery, Result<PaginationDto<LearningPathSummaryDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetLearningPathSummaryByUserIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginationDto<LearningPathSummaryDto>>> Handle(
        GetLearningPathSummaryByUserIdQuery request,
        CancellationToken cancellationToken)
    {
        var userExists = await _context.Users.AnyAsync(u => u.UserId == request.UserId, cancellationToken);
        if (!userExists)
            return Result<PaginationDto<LearningPathSummaryDto>>.Failure("USER_NOT_FOUND", "User not found.");

        var userId = request.UserId;

        var query = _context.LearningPaths
            .AsNoTracking()
            .Where(lp => lp.UserId == userId);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(lp =>
                lp.Title.ToLower().Contains(term) ||
                (lp.Description != null && lp.Description.ToLower().Contains(term)));
        }

        if (request.SubjectId.HasValue)
            query = query.Where(lp => lp.SubjectId == request.SubjectId.Value);

        if (request.Status.HasValue)
            query = query.Where(lp => lp.Status == request.Status.Value.ToString());

        var totalCount = await query.CountAsync(cancellationToken);

        query = request.SortDescending
            ? query.OrderByDescending(lp => lp.CreatedAt)
            : query.OrderBy(lp => lp.CreatedAt);

        var paths = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(lp => new
            {
                lp.PathId,
                lp.Title,
                lp.Description,
                lp.Status,
                lp.StartDate,
                lp.EndDate,
                lp.CreatedAt,
                lp.ComplexityLevel,
                Language = lp.Language,
                ChapterCount = lp.Chapters.Count(c => !c.IsDeleted),
                // lesson contents
                TotalLessons = lp.Chapters.Where(c => !c.IsDeleted)
                    .SelectMany(c => c.Lessons.Where(l => !l.IsDeleted)).Count(),
                CompletedLessons = lp.Chapters.Where(c => !c.IsDeleted)
                    .SelectMany(c => c.Lessons.Where(l => !l.IsDeleted))
                    .Count(l => l.LearnProgresses.Any(lp2 => lp2.UserId == userId && lp2.IsLessonContentRead)),
                // quizzes
                TotalQuizzes = lp.Chapters.Where(c => !c.IsDeleted)
                    .SelectMany(c => c.Lessons.Where(l => !l.IsDeleted))
                    .SelectMany(l => l.Quizzes.Where(q => !q.IsDeleted)).Count(),
                CompletedQuizzes = lp.Chapters.Where(c => !c.IsDeleted)
                    .SelectMany(c => c.Lessons.Where(l => !l.IsDeleted))
                    .SelectMany(l => l.Quizzes.Where(q => !q.IsDeleted))
                    .Count(q => q.QuizAttempts.Any(a => a.UserId == userId && a.Status == QuizAttemptStatus.Passed)),
                // tasks
                TotalTasks = lp.Chapters.Where(c => !c.IsDeleted)
                    .SelectMany(c => c.Tasks.Where(t => !t.IsDeleted)).Count(),
                CompletedTasks = lp.Chapters.Where(c => !c.IsDeleted)
                    .SelectMany(c => c.Tasks.Where(t => !t.IsDeleted))
                    .Count(t => t.Status == TaskStatus_.Completed)
            })
            .ToListAsync(cancellationToken);

        var items = paths.Select(p =>
        {
            var totalItems = p.TotalLessons + p.TotalQuizzes + p.TotalTasks;
            var completedItems = p.CompletedLessons + p.CompletedQuizzes + p.CompletedTasks;
            var progressPercent = totalItems == 0
                ? 0m
                : Math.Min(100m, Math.Round(completedItems * 100m / totalItems, 2));

            return new LearningPathSummaryDto(
                p.PathId,
                p.Title,
                p.Description,
                p.Status,
                p.ChapterCount,
                progressPercent,
                p.StartDate,
                p.EndDate,
                p.CreatedAt,
                p.ComplexityLevel,
                p.Language);
        }).ToList();

        return Result<PaginationDto<LearningPathSummaryDto>>.Success(new PaginationDto<LearningPathSummaryDto>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        });
    }
}
