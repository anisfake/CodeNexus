using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
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
            .Include(lp => lp.LearningPathGoals)
                .ThenInclude(lpg => lpg.Goal)
            .Include(lp => lp.UserGoalProgresses.Where(ugp => ugp.UserId == request.UserId))
            .Include(lp => lp.User)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
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

        var pagedQuery = query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize);

        var items = await (
            from lp in pagedQuery
            let latestShare = _context.LearningPathShares
                .Where(s => s.AcceptedPathId == lp.PathId && s.Status == LearningPathShareStatus.Accepted)
                .OrderByDescending(s => s.RespondedAt ?? s.SentAt)
                .Select(s => new
                {
                    MentorId = (Guid?)s.MentorId,
                    MentorUserName = s.Mentor.Username,
                    SourceLearningPathId = (Guid?)s.PathId,
                    SourceVersion = s.SourceVersionAtAccept,
                    SourceLatestVersion = (decimal?)s.LearningPath.VersionNumber,
                    HasSourceUpdate = (s.SourceVersionAtAccept ?? 1.0m) < s.LearningPath.VersionNumber
                        && s.IsTrackingEnabled
                        && (!s.IgnoredSourceVersion.HasValue || s.IgnoredSourceVersion.Value < s.LearningPath.VersionNumber)
                })
                .FirstOrDefault()
            select new LearningPathResponse(
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
                        lp.UserGoalProgresses.FirstOrDefault(ugp => ugp.GoalId == g.GoalId && ugp.UserId == request.UserId) != null
                            ? lp.UserGoalProgresses.FirstOrDefault(ugp => ugp.GoalId == g.GoalId && ugp.UserId == request.UserId)!.Status.ToString()
                            : GoalProgressStatus.NotStarted.ToString(),
                        lp.UserGoalProgresses.FirstOrDefault(ugp => ugp.GoalId == g.GoalId && ugp.UserId == request.UserId) != null
                            ? lp.UserGoalProgresses.FirstOrDefault(ugp => ugp.GoalId == g.GoalId && ugp.UserId == request.UserId)!.CompletedAt
                            : null,
                        lp.UserGoalProgresses.FirstOrDefault(ugp => ugp.GoalId == g.GoalId && ugp.UserId == request.UserId) != null
                            ? lp.UserGoalProgresses.FirstOrDefault(ugp => ugp.GoalId == g.GoalId && ugp.UserId == request.UserId)!.ProgressPercent
                            : 0m,
                        g.Weight * 100m
                    )).ToList(),
                lp.StartDate,
                lp.EndDate,
                lp.Title,
                lp.Description,
                lp.Status.ToString(),
                lp.CreatedByType,
                lp.UserId,
                lp.User.Username,
                lp.Chapters.Where(c => !c.IsDeleted).Select(c => new ChapterDto(
                    c.ChapterId,
                    c.Title,
                    c.Content,
                    c.OrderIndex,
                    c.Lessons.Where(l => !l.IsDeleted).Select(l => new LessonDto(
                        l.LessonId,
                        l.Title,
                        l.Content,
                        l.LessonDay,
                        l.Quizzes.Where(q => !q.IsDeleted).Select(q => new QuizDto(
                            q.QuizId,
                            q.Title,
                            q.Description,
                            null,
                            q.QuizAttempts.FirstOrDefault(qa => qa.UserId == request.UserId) != null
                                ? q.QuizAttempts.FirstOrDefault(qa => qa.UserId == request.UserId)!.Status.ToString()
                                : "Not Attempted"
                        )).ToList(),
                        l.LearnProgresses.FirstOrDefault(lp => lp.UserId == request.UserId) != null
                            ? l.LearnProgresses.FirstOrDefault(lp => lp.UserId == request.UserId)!.IsLessonContentRead
                                ? "Completed"
                                : "In Progress"
                            : "Not Started"
                    )).ToList(),
                    c.Tasks.Where(t => !t.IsDeleted).Select(t => new TaskDto(
                        t.TaskId,
                        t.Title,
                        t.Description,
                        t.TaskType,
                        t.Priority,
                        t.Status,
                        t.DueDate,
                        t.QuizQuestionsJson,
                        t.Status.ToString()
                    )).ToList()
                )).ToList(),
                lp.Chapters.Count(c => !c.IsDeleted),
                lp.CreatedAt,
                lp.ComplexityLevel,
                lp.Language,
                latestShare != null ? latestShare.MentorId : null,
                latestShare != null ? latestShare.MentorUserName : null,
                latestShare != null ? latestShare.SourceLearningPathId : null,
                latestShare != null ? latestShare.SourceVersion : null,
                latestShare != null ? latestShare.SourceLatestVersion : null,
                latestShare != null && latestShare.HasSourceUpdate
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
