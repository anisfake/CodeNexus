using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Common.Events;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateMentorLearningPathDraft;

public class UpdateMentorLearningPathDraftCommandHandler : IRequestHandler<UpdateMentorLearningPathDraftCommand, Result<CreateLearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublisher _publisher;

    public UpdateMentorLearningPathDraftCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPublisher publisher)
    {
        _context = context;
        _currentUserService = currentUserService;
        _publisher = publisher;
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
            .Join(goals, ng => ng.GoalId, g => g.GoalId, (ng, g) => (Goal: g, Weight: ng.Weight))
            .OrderByDescending(g => g.Weight)
            .ToList();

        if (IsNoDraftChange(request, learningPath, goalsWithWeights))
        {
            var currentChapterDtos = BuildChapterDtosFromCurrent(learningPath);
            var currentGoalDtos = goalsWithWeights
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
                currentGoalDtos,
                currentChapterDtos,
                currentChapterDtos.Count,
                learningPath.CreatedAt,
                false,
                learningPath.StartDate,
                learningPath.EndDate,
                learningPath.ComplexityLevel,
                learningPath.Language,
                learningPath.SubjectId,
                subject.Name));
        }

        var nextVersion = learningPath.VersionNumber + 1;

        learningPath.SubjectId = request.SubjectId;
        learningPath.Title = BuildVersionedTitle(request.Title, nextVersion);
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

        learningPath.VersionNumber = nextVersion;
        var currentVersion = learningPath.VersionNumber;

        await _context.SaveChangesAsync(cancellationToken);
        await _publisher.Publish(
            new LearningPathDraftVersionUpdatedEvent(
                learningPath.PathId,
                mentor.UserId,
                mentor.Username,
                currentVersion,
                now),
            cancellationToken);

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

    private static bool IsNoDraftChange(
        UpdateMentorLearningPathDraftCommand request,
        LearningPath learningPath,
        IReadOnlyCollection<(GoalEntity Goal, decimal Weight)> requestedGoalsWithWeights)
    {
        if (request.SubjectId != learningPath.SubjectId)
        {
            return false;
        }

        if (!string.Equals(NormalizeBaseTitle(request.Title), NormalizeBaseTitle(learningPath.Title), StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(NormalizeOptionalText(request.Description), NormalizeOptionalText(learningPath.Description), StringComparison.Ordinal))
        {
            return false;
        }

        if (request.StartDate != learningPath.StartDate || request.EndDate != learningPath.EndDate)
        {
            return false;
        }

        if (request.ComplexityLevel != learningPath.ComplexityLevel || request.LanguageSelection != learningPath.Language)
        {
            return false;
        }

        if (!GoalsMatchCurrent(learningPath, requestedGoalsWithWeights))
        {
            return false;
        }

        return ChaptersMatchCurrent(request.Chapters, learningPath);
    }

    private static bool GoalsMatchCurrent(
        LearningPath learningPath,
        IReadOnlyCollection<(GoalEntity Goal, decimal Weight)> requestedGoalsWithWeights)
    {
        if (learningPath.LearningPathGoals.Count != requestedGoalsWithWeights.Count)
        {
            return false;
        }

        var currentGoalWeights = learningPath.LearningPathGoals
            .ToDictionary(x => x.GoalId, x => x.Weight);

        foreach (var requestedGoal in requestedGoalsWithWeights)
        {
            if (!currentGoalWeights.TryGetValue(requestedGoal.Goal.GoalId, out var currentWeight))
            {
                return false;
            }

            if (currentWeight != requestedGoal.Weight)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ChaptersMatchCurrent(List<ManualChapterRequest> requestedChapters, LearningPath learningPath)
    {
        var currentChapters = learningPath.Chapters
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.OrderIndex)
            .ToList();

        if (currentChapters.Count != requestedChapters.Count)
        {
            return false;
        }

        for (int chapterIndex = 0; chapterIndex < requestedChapters.Count; chapterIndex++)
        {
            var requestedChapter = requestedChapters[chapterIndex];
            var currentChapter = currentChapters[chapterIndex];

            if (!string.Equals(NormalizeRequiredText(requestedChapter.Title), NormalizeRequiredText(currentChapter.Title), StringComparison.Ordinal))
            {
                return false;
            }

            if (requestedChapter.StartDate != currentChapter.StartDate || requestedChapter.EndDate != currentChapter.EndDate)
            {
                return false;
            }

            var expectedEstimatedDays = requestedChapter.EstimatedDays
                ?? CalculateEstimatedDays(requestedChapter.StartDate, requestedChapter.EndDate);

            if (currentChapter.EstimatedDays != expectedEstimatedDays)
            {
                return false;
            }

            var currentLessons = currentChapter.Lessons
                .Where(l => !l.IsDeleted)
                .OrderBy(l => l.OrderIndex)
                .ToList();

            if (currentLessons.Count != requestedChapter.Lessons.Count)
            {
                return false;
            }

            for (int lessonIndex = 0; lessonIndex < requestedChapter.Lessons.Count; lessonIndex++)
            {
                var requestedLesson = requestedChapter.Lessons[lessonIndex];
                var currentLesson = currentLessons[lessonIndex];

                if (!string.Equals(NormalizeRequiredText(requestedLesson.Title), NormalizeRequiredText(currentLesson.Title), StringComparison.Ordinal))
                {
                    return false;
                }

                if (requestedLesson.LessonDay != currentLesson.LessonDay)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static List<ChapterDto> BuildChapterDtosFromCurrent(LearningPath learningPath)
    {
        return learningPath.Chapters
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.OrderIndex)
            .Select(chapter => new ChapterDto(
                chapter.ChapterId,
                chapter.Title,
                chapter.Content,
                chapter.OrderIndex,
                chapter.Lessons
                    .Where(lesson => !lesson.IsDeleted)
                    .OrderBy(lesson => lesson.OrderIndex)
                    .Select(lesson => new LessonDto(
                        lesson.LessonId,
                        lesson.Title,
                        lesson.Content,
                        lesson.LessonDay,
                        new List<QuizDto>()))
                    .ToList(),
                new List<TaskDto>()))
            .ToList();
    }

    private static string NormalizeOptionalText(string? text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();

    private static string NormalizeRequiredText(string? text)
        => string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();

    private static string NormalizeBaseTitle(string? rawTitle)
    {
        var baseTitle = string.IsNullOrWhiteSpace(rawTitle)
            ? "Learning Path"
            : rawTitle.Trim();

        baseTitle = Regex.Replace(baseTitle, @"\s*-\s*ver\s+\d+\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        baseTitle = Regex.Replace(baseTitle, @"\s+v\d+\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();

        return baseTitle;
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

    private static string BuildVersionedTitle(string rawTitle, int versionNumber)
    {
        return $"{NormalizeBaseTitle(rawTitle)} - ver {versionNumber}";
    }
}
