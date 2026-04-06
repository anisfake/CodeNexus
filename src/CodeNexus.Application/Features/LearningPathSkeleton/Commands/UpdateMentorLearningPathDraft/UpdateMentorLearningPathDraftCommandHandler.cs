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
            return Result<CreateLearningPathResponse>.Failure("ACCESS_DENIED", "Access denied.");
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
            return Result<CreateLearningPathResponse>.Failure("ACCESS_DENIED", "Access denied.");
        }

        if (!string.Equals(learningPath.Status, LearningPathStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<CreateLearningPathResponse>.Failure("INVALID_STATUS", "Invalid status for this operation.");
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
            return Result<CreateLearningPathResponse>.Failure("GOAL_NOT_FOUND", "Goal not found.");
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
                    "Goal is not relevant to the selected subject.");
            }
        }

        var normalizedGoals = NormalizeGoalWeights(request.Goals);
        var goalsWithWeights = normalizedGoals
            .Join(goals, ng => ng.GoalId, g => g.GoalId, (ng, g) => new { Goal = g, ng.Weight })
            .OrderByDescending(g => g.Weight)
            .ToList();

        var goalDtos = goalsWithWeights
            .Select(g => new LearningPathGoalDto(
                g.Goal.GoalId,
                g.Goal.Title,
                g.Weight,
                g.Goal.DurationInDays,
                "NotStarted",
                null))
            .ToList();

        if (!HasDraftChanges(learningPath, request, normalizedGoals))
        {
            var existingChapterDtos = BuildExistingChapterDtos(learningPath);

            return Result<CreateLearningPathResponse>.Success(new CreateLearningPathResponse(
                learningPath.PathId,
                learningPath.Title,
                learningPath.Description ?? string.Empty,
                goalDtos,
                existingChapterDtos,
                existingChapterDtos.Count,
                learningPath.CreatedAt,
                false,
                learningPath.StartDate,
                learningPath.EndDate,
                learningPath.ComplexityLevel,
                learningPath.Language,
                learningPath.SubjectId,
                subject.Name));
        }

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

        return Result<CreateLearningPathResponse>.Success(new CreateLearningPathResponse(
            learningPath.PathId,
            learningPath.Title,
            learningPath.Description ?? string.Empty,
            goalDtos,
            chapterDtos,
            chapterDtos.Count,
            learningPath.CreatedAt,
            false,
            learningPath.StartDate,
            learningPath.EndDate,
            learningPath.ComplexityLevel,
            learningPath.Language,
            learningPath.SubjectId,
            subject.Name));
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

    private static bool HasDraftChanges(
        LearningPath learningPath,
        UpdateMentorLearningPathDraftCommand request,
        List<LearningPathGoalRequest> normalizedGoals)
    {
        var normalizedTitle = request.Title.Trim();
        var normalizedDescription = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (learningPath.SubjectId != request.SubjectId
            || !string.Equals(learningPath.Title, normalizedTitle, StringComparison.Ordinal)
            || !string.Equals(learningPath.Description, normalizedDescription, StringComparison.Ordinal)
            || learningPath.StartDate != request.StartDate
            || learningPath.EndDate != request.EndDate
            || learningPath.ComplexityLevel != request.ComplexityLevel
            || learningPath.Language != request.LanguageSelection)
        {
            return true;
        }

        var existingGoals = learningPath.LearningPathGoals
            .Select(g => new LearningPathGoalRequest(g.GoalId, Math.Round(g.Weight, 2)))
            .OrderBy(g => g.GoalId)
            .ToList();

        var incomingGoals = normalizedGoals
            .Select(g => new LearningPathGoalRequest(g.GoalId, Math.Round(g.Weight, 2)))
            .OrderBy(g => g.GoalId)
            .ToList();

        if (existingGoals.Count != incomingGoals.Count)
        {
            return true;
        }

        for (int i = 0; i < existingGoals.Count; i++)
        {
            if (existingGoals[i].GoalId != incomingGoals[i].GoalId
                || existingGoals[i].Weight != incomingGoals[i].Weight)
            {
                return true;
            }
        }

        var existingChapters = learningPath.Chapters
            .OrderBy(c => c.OrderIndex)
            .ToList();

        if (existingChapters.Count != request.Chapters.Count)
        {
            return true;
        }

        for (int chapterIndex = 0; chapterIndex < request.Chapters.Count; chapterIndex++)
        {
            var existingChapter = existingChapters[chapterIndex];
            var requestChapter = request.Chapters[chapterIndex];

            if (existingChapter.OrderIndex != chapterIndex
                || !string.Equals(existingChapter.Title, requestChapter.Title.Trim(), StringComparison.Ordinal)
                || existingChapter.StartDate != requestChapter.StartDate
                || existingChapter.EndDate != requestChapter.EndDate
                || existingChapter.EstimatedDays != (requestChapter.EstimatedDays ?? CalculateEstimatedDays(requestChapter.StartDate, requestChapter.EndDate)))
            {
                return true;
            }

            var existingLessons = existingChapter.Lessons
                .OrderBy(l => l.OrderIndex)
                .ToList();

            if (existingLessons.Count != requestChapter.Lessons.Count)
            {
                return true;
            }

            for (int lessonIndex = 0; lessonIndex < requestChapter.Lessons.Count; lessonIndex++)
            {
                var existingLesson = existingLessons[lessonIndex];
                var requestLesson = requestChapter.Lessons[lessonIndex];

                if (existingLesson.OrderIndex != lessonIndex
                    || !string.Equals(existingLesson.Title, requestLesson.Title.Trim(), StringComparison.Ordinal)
                    || existingLesson.LessonDay != requestLesson.LessonDay)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<ChapterDto> BuildExistingChapterDtos(LearningPath learningPath)
    {
        return learningPath.Chapters
            .OrderBy(c => c.OrderIndex)
            .Select(c => new ChapterDto(
                c.ChapterId,
                c.Title,
                c.Content,
                c.OrderIndex,
                c.Lessons
                    .OrderBy(l => l.OrderIndex)
                    .Select(l => new LessonDto(
                        l.LessonId,
                        l.Title,
                        l.Content,
                        l.LessonDay,
                        new List<QuizDto>()))
                    .ToList(),
                new List<TaskDto>()))
            .ToList();
    }
}
