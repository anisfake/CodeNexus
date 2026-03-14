using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using CodeNexus.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;

public class GenerateLearningPathSkeletonCommandHandler : IRequestHandler<GenerateLearningPathSkeletonCommand, Result<CreateLearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITimelineCalculationService _timelineCalculationService;

    public GenerateLearningPathSkeletonCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ITimelineCalculationService timelineCalculationService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _timelineCalculationService = timelineCalculationService;
    }

    public async Task<Result<CreateLearningPathResponse>> Handle(GenerateLearningPathSkeletonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var subject = await _context.Subjects.FirstOrDefaultAsync(x => x.SubjectId == request.SubjectId, cancellationToken: cancellationToken);
            if (subject == null)
            {
                return Result<CreateLearningPathResponse>.Failure("SUBJECT_NOT_FOUND", "Subject not found");
            }

            var goal = await _context.Goals
                .FirstOrDefaultAsync(x => x.GoalId == request.GoalId, cancellationToken: cancellationToken);
            if (goal == null)
            {
                return Result<CreateLearningPathResponse>.Failure("GOAL_NOT_FOUND", "Goal not found");
            }

            // Create learning path with timeline-based structure
            var learningPath = new LearningPath
            {
                PathId = NewId.NextGuid(),
                UserId = userId,
                SubjectId = request.SubjectId,
                GoalId = request.GoalId,
                Title = $"Learning Path: {subject.Name} - {goal.Title}",
                Description = $"Complete learning path for {subject.Name} to achieve: {goal.Title}",
                Status = "Active",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(goal.DurationInDays),
                CreatedAt = DateTime.UtcNow,
                CreatedByType = true,
                Language = request.LanguageSelection
            };

            await _context.LearningPaths.AddAsync(learningPath, cancellationToken);

            // Calculate chapter timelines (1 chapter = 1 week)
            var chapterTimelines = await _timelineCalculationService.CalculateChapterTimelinesAsync(
                learningPath.StartDate!.Value,
                learningPath.EndDate!.Value,
                0, // Not used anymore, calculated inside service
                request.ComplexityLevel,
                cancellationToken);

            var chapters = new List<ChapterDto>();
            for (int i = 0; i < chapterTimelines.Count; i++)
            {
                var chapterTimeline = chapterTimelines[i];

                var chapter = new Chapter
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = learningPath.PathId,
                    Title = $"Chapter {i + 1}: {subject.Name} Fundamentals {i + 1}",
                    OrderIndex = i,
                    IsCompleted = false,
                    StartDate = chapterTimeline.StartDate,
                    EndDate = chapterTimeline.EndDate,
                    EstimatedDays = chapterTimeline.EstimatedDays,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Chapters.AddAsync(chapter, cancellationToken);

                // Calculate lesson schedules based on complexity
                var lessonSchedules = await _timelineCalculationService.CalculateLessonSchedulesAsync(
                    chapterTimeline.StartDate,
                    chapterTimeline.EndDate,
                    0, // Not used anymore, calculated inside service
                    request.ComplexityLevel,
                    cancellationToken);

                var lessonDtos = new List<LessonDto>();
                var quizDtos = new List<QuizDto>();

                for (int j = 0; j < lessonSchedules.Count; j++)
                {
                    var lessonSchedule = lessonSchedules[j];

                    var lesson = new Lesson
                    {
                        LessonId = NewId.NextGuid(),
                        ChapterId = chapter.ChapterId,
                        Title = $"Lesson {j + 1}: {subject.Name} Topic {j + 1}",
                        Content = string.Empty,
                        OrderIndex = j,
                        LessonDay = lessonSchedule.LessonDay, // Use LessonDay instead of ScheduledDate
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Lessons.AddAsync(lesson, cancellationToken);

                    // Every lesson has quizzes now
                    var quizzesPerLesson = _timelineCalculationService.GetQuizzesPerLesson(request.ComplexityLevel);
                    var lessonQuizzes = new List<QuizDto>();

                    for (int k = 0; k < quizzesPerLesson; k++)
                    {
                        var quiz = new Quiz
                        {
                            QuizId = NewId.NextGuid(),
                            LessonId = lesson.LessonId,
                            Title = $"Quiz {k + 1}: {lesson.Title}",
                            Description = $"Assessment quiz for {lesson.Title}",
                            DueDate = lessonSchedule.LessonDay.AddDays(2), // Due 2 days after lesson day
                            CreatedAt = DateTime.UtcNow
                        };

                        await _context.Quizzes.AddAsync(quiz, cancellationToken);

                        lessonQuizzes.Add(new QuizDto(
                            quiz.QuizId,
                            quiz.Title,
                            quiz.Description
                        ));
                    }

                    lessonDtos.Add(new LessonDto(
                        lesson.LessonId,
                        lesson.Title,
                        lesson.Content,
                        lessonQuizzes
                    ));

                    quizDtos.AddRange(lessonQuizzes);
                }

                chapters.Add(new ChapterDto(
                    chapter.ChapterId,
                    chapter.Title,
                    null,
                    chapter.OrderIndex,
                    lessonDtos,
                    new List<TaskDto>()
                ));
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result<CreateLearningPathResponse>.Success(
                new CreateLearningPathResponse(
                    learningPath.PathId,
                    learningPath.Title,
                    learningPath.Description,
                    chapters,
                    chapterTimelines.Count,
                    learningPath.CreatedAt,
                    true
                )
            );
        }
        catch (Exception ex)
        {
            return Result<CreateLearningPathResponse>.Failure("GENERATION_FAILED", $"Failed to generate learning path skeleton: {ex.Message}");
        }
    }

}