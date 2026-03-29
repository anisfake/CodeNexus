using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateMentorLearningPathDraft;

public class UpdateMentorLearningPathDraftCommandHandler : IRequestHandler<UpdateMentorLearningPathDraftCommand, Result<CreateLearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateMentorLearningPathDraftCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<CreateLearningPathResponse>> Handle(UpdateMentorLearningPathDraftCommand request, CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<CreateLearningPathResponse>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var mentor = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == mentorId, cancellationToken);

        if (mentor == null)
        {
            return Result<CreateLearningPathResponse>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateLearningPathResponse>.Failure("ACCESS_DENIED", "Only mentors can update learning path drafts.");
        }

        var learningPath = await _context.LearningPaths
            .Include(lp => lp.LearningPathGoals)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (learningPath == null)
        {
            return Result<CreateLearningPathResponse>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (learningPath.UserId != mentorId)
        {
            return Result<CreateLearningPathResponse>.Failure("ACCESS_DENIED", "You can only update your own learning path drafts.");
        }

        if (!string.Equals(learningPath.Status, LearningPathStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateLearningPathResponse>.Failure("INVALID_STATUS", "Only draft learning paths can be updated.");
        }

        var subject = await _context.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubjectId == request.SubjectId, cancellationToken);

        if (subject == null)
        {
            return Result<CreateLearningPathResponse>.Failure("SUBJECT_NOT_FOUND", "Subject not found.");
        }

        var uniqueGoalIds = request.Goals.Select(g => g.GoalId).Distinct().ToList();
        var goals = await _context.Goals
            .Where(g => uniqueGoalIds.Contains(g.GoalId))
            .ToListAsync(cancellationToken);

        if (goals.Count != uniqueGoalIds.Count)
        {
            return Result<CreateLearningPathResponse>.Failure("GOAL_NOT_FOUND", "One or more goals were not found.");
        }

        var systemGoalIds = goals
            .Where(g => g.IsSystemDefined)
            .Select(g => g.GoalId)
            .ToList();

        if (systemGoalIds.Count > 0)
        {
            var mappedSystemGoalIds = await _context.SubjectGoals
                .Where(sg => sg.SubjectId == request.SubjectId && systemGoalIds.Contains(sg.GoalId))
                .Select(sg => sg.GoalId)
                .ToListAsync(cancellationToken);

            if (mappedSystemGoalIds.Count != systemGoalIds.Count)
            {
                return Result<CreateLearningPathResponse>.Failure(
                    "GOAL_SUBJECT_MISMATCH",
                    "One or more system goals are not available for the selected subject.");
            }
        }

        var normalizedGoals = NormalizeGoalWeights(request.Goals);
        var goalsWithWeights = normalizedGoals
            .Join(goals, ng => ng.GoalId, g => g.GoalId, (ng, g) => new { Goal = g, ng.Weight })
            .OrderByDescending(g => g.Weight)
            .ToList();

        learningPath.SubjectId = request.SubjectId;
        learningPath.Title = request.Title.Trim();
        learningPath.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        learningPath.StartDate = request.StartDate;
        learningPath.EndDate = request.EndDate;
        learningPath.ComplexityLevel = request.ComplexityLevel;
        learningPath.Language = request.LanguageSelection;
        learningPath.Status = LearningPathStatus.Draft.ToString();

        _context.LearningPathGoals.RemoveRange(learningPath.LearningPathGoals);

        foreach (var goalWithWeight in goalsWithWeights)
        {
            await _context.LearningPathGoals.AddAsync(new LearningPathGoal
            {
                PathId = learningPath.PathId,
                GoalId = goalWithWeight.Goal.GoalId,
                Weight = goalWithWeight.Weight
            }, cancellationToken);
        }

        var now = DateTime.UtcNow;
        foreach (var existingChapter in learningPath.Chapters)
        {
            existingChapter.IsDeleted = true;
            existingChapter.DeletedAt = now;
            existingChapter.UpdatedAt = now;

            foreach (var existingLesson in existingChapter.Lessons)
            {
                existingLesson.IsDeleted = true;
                existingLesson.DeletedAt = now;
                existingLesson.UpdatedAt = now;
            }
        }

        var chapterDtos = new List<ChapterDto>();
        for (int i = 0; i < request.Chapters.Count; i++)
        {
            var chapterRequest = request.Chapters[i];

            var chapter = new Chapter
            {
                ChapterId = NewId.NextGuid(),
                PathId = learningPath.PathId,
                Title = chapterRequest.Title.Trim(),
                OrderIndex = i,
                IsCompleted = false,
                StartDate = chapterRequest.StartDate,
                EndDate = chapterRequest.EndDate,
                EstimatedDays = chapterRequest.EstimatedDays ?? CalculateEstimatedDays(chapterRequest.StartDate, chapterRequest.EndDate),
                CreatedAt = now
            };

            await _context.Chapters.AddAsync(chapter, cancellationToken);

            var lessonDtos = new List<LessonDto>();
            for (int j = 0; j < chapterRequest.Lessons.Count; j++)
            {
                var lessonRequest = chapterRequest.Lessons[j];

                var lesson = new Lesson
                {
                    LessonId = NewId.NextGuid(),
                    ChapterId = chapter.ChapterId,
                    Title = lessonRequest.Title.Trim(),
                    Content = string.Empty,
                    OrderIndex = j,
                    LessonDay = lessonRequest.LessonDay,
                    CreatedAt = now
                };

                await _context.Lessons.AddAsync(lesson, cancellationToken);

                lessonDtos.Add(new LessonDto(
                    lesson.LessonId,
                    lesson.Title,
                    lesson.Content,
                    lesson.LessonDay,
                    new List<QuizDto>()));
            }

            chapterDtos.Add(new ChapterDto(
                chapter.ChapterId,
                chapter.Title,
                chapter.Content,
                chapter.OrderIndex,
                lessonDtos,
                new List<TaskDto>()));
        }

        await _context.SaveChangesAsync(cancellationToken);

        var goalDtos = goalsWithWeights
            .Select(g => new LearningPathGoalDto(
                g.Goal.GoalId,
                g.Goal.Title,
                g.Weight,
                g.Goal.DurationInDays,
                "NotStarted",
                null))
            .ToList();

        return Result<CreateLearningPathResponse>.Success(new CreateLearningPathResponse(
            learningPath.PathId,
            learningPath.Title,
            learningPath.Description ?? string.Empty,
            goalDtos,
            chapterDtos,
            chapterDtos.Count,
            learningPath.CreatedAt,
            false));
    }

    private static int CalculateEstimatedDays(DateTime? startDate, DateTime? endDate)
    {
        if (!startDate.HasValue || !endDate.HasValue)
        {
            return 7;
        }

        var days = (int)Math.Ceiling((endDate.Value.Date - startDate.Value.Date).TotalDays) + 1;
        return Math.Max(days, 1);
    }

    private static List<LearningPathGoalRequest> NormalizeGoalWeights(List<LearningPathGoalRequest> goals)
    {
        var total = goals.Sum(g => g.Weight);
        if (total <= 0)
        {
            return goals;
        }

        var normalized = goals
            .Select(g => new LearningPathGoalRequest(g.GoalId, Math.Round((g.Weight / total) * 100m, 2)))
            .ToList();

        var diff = 100m - normalized.Sum(g => g.Weight);
        if (normalized.Count > 0 && diff != 0)
        {
            var top = normalized[0];
            normalized[0] = top with { Weight = top.Weight + diff };
        }

        return normalized;
    }
}
