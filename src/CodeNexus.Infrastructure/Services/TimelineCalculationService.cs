using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Infrastructure.Services;

public class TimelineCalculationService : ITimelineCalculationService
{
    public async Task<List<ChapterTimelineDto>> CalculateChapterTimelinesAsync(
        DateTime learningPathStartDate,
        DateTime learningPathEndDate,
        int totalChapters,
        ComplexityLevel complexity,
        CancellationToken cancellationToken = default)
    {
        var totalDays = (learningPathEndDate - learningPathStartDate).Days;
        var chapterTimelines = new List<ChapterTimelineDto>();

        const int daysPerChapter = 7;

        var calculatedChapters = (int)Math.Ceiling((double)totalDays / daysPerChapter);

        var actualChapterCount = calculatedChapters;

        var currentDate = learningPathStartDate;

        for (int i = 0; i < actualChapterCount; i++)
        {
            var endDate = currentDate.AddDays(daysPerChapter - 1);

            if (i == actualChapterCount - 1)
            {
                endDate = learningPathEndDate;
            }

            var actualDaysForChapter = (endDate - currentDate).Days + 1;

            chapterTimelines.Add(new ChapterTimelineDto(
                OrderIndex: i,
                StartDate: currentDate,
                EndDate: endDate,
                EstimatedDays: actualDaysForChapter
            ));

            currentDate = endDate.AddDays(1);
        }

        return await Task.FromResult(chapterTimelines);
    }

    public async Task<List<LessonScheduleDto>> CalculateLessonSchedulesAsync(
        DateTime chapterStartDate,
        DateTime chapterEndDate,
        int totalLessons,
        ComplexityLevel complexity,
        CancellationToken cancellationToken = default)
    {
        var lessonSchedules = new List<LessonScheduleDto>();

        var lessonsPerChapter = GetLessonsPerChapter(complexity);

        var actualLessonCount = lessonsPerChapter;

        const int availableDaysForLessons = 5;
        var daysPerLesson = Math.Max(1, availableDaysForLessons / actualLessonCount);

        var estimatedMinutes = GetEstimatedMinutesPerLesson(complexity);
        var currentDate = chapterStartDate;

        for (int i = 0; i < actualLessonCount; i++)
        {
            lessonSchedules.Add(new LessonScheduleDto(
                OrderIndex: i,
                ScheduledDate: currentDate,
                EstimatedMinutes: estimatedMinutes
            ));

            currentDate = currentDate.AddDays(daysPerLesson);
        }

        return await Task.FromResult(lessonSchedules);
    }

    public async Task<List<TaskScheduleDto>> CalculateTaskSchedulesAsync(
        DateTime chapterStartDate,
        DateTime chapterEndDate,
        int totalTasks,
        ComplexityLevel complexity,
        CancellationToken cancellationToken = default)
    {
        var taskSchedules = new List<TaskScheduleDto>();

        if (totalTasks == 0) return taskSchedules;

        var taskStartDate = chapterStartDate.AddDays(5);
        var taskEndDate = chapterEndDate;

        var availableDays = (taskEndDate - taskStartDate).Days + 1;
        var daysPerTask = Math.Max(1, availableDays / totalTasks);
        var currentDate = taskStartDate;

        for (int i = 0; i < totalTasks; i++)
        {
            var dueDate = currentDate.AddDays(daysPerTask - 1);

            if (i == totalTasks - 1)
            {
                dueDate = taskEndDate;
            }

            taskSchedules.Add(new TaskScheduleDto(
                OrderIndex: i,
                DueDate: dueDate
            ));

            currentDate = currentDate.AddDays(daysPerTask);
        }

        return await Task.FromResult(taskSchedules);
    }

    public async Task<List<QuizScheduleDto>> CalculateQuizSchedulesAsync(
        List<LessonScheduleDto> lessonSchedules,
        int totalQuizzes,
        CancellationToken cancellationToken = default)
    {
        var quizSchedules = new List<QuizScheduleDto>();

        if (!lessonSchedules.Any()) return quizSchedules;

        foreach (var lesson in lessonSchedules)
        {
            var availableFrom = lesson.ScheduledDate.AddHours(1);
            var dueDate = lesson.ScheduledDate.AddDays(2);

            quizSchedules.Add(new QuizScheduleDto(
                LessonOrderIndex: lesson.OrderIndex,
                AvailableFrom: availableFrom,
                DueDate: dueDate
            ));
        }

        return await Task.FromResult(quizSchedules);
    }
    private int GetLessonsPerChapter(ComplexityLevel complexity)
    {
        return complexity switch
        {
            ComplexityLevel.Beginner => 4,      // 4 lessons per chapter 
            ComplexityLevel.Intermediate => 5,   // 5 lessons per chapter 
            ComplexityLevel.Advanced => 6,       // 6 lessons per chapter 
            _ => 4
        };
    }
    private int GetEstimatedMinutesPerLesson(ComplexityLevel complexity)
    {
        return complexity switch
        {
            ComplexityLevel.Beginner => 45,
            ComplexityLevel.Intermediate => 60,
            ComplexityLevel.Advanced => 90,
            _ => 60
        };
    }
    public int GetQuizzesPerLesson(ComplexityLevel complexity)
    {
        return complexity switch
        {
            ComplexityLevel.Beginner => 1,
            ComplexityLevel.Intermediate => 1,
            ComplexityLevel.Advanced => 2,
            _ => 1
        };
    }
}