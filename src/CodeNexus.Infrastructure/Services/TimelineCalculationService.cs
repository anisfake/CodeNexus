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

        var chapterDays = (chapterEndDate - chapterStartDate).Days + 1;

        var lessonDurations = GetLessonDurations(actualLessonCount, chapterDays, complexity);

        var currentDay = 1;

        for (int i = 0; i < actualLessonCount; i++)
        {
            var lessonDuration = lessonDurations[i];
            var lessonDay = currentDay + lessonDuration - 1;

            if (lessonDay > chapterDays)
            {
                lessonDay = chapterDays;
            }

            var actualLessonDate = chapterStartDate.AddDays(lessonDay - 1);

            lessonSchedules.Add(new LessonScheduleDto(
                OrderIndex: i,
                LessonDay: actualLessonDate
            ));

            currentDay = lessonDay + 1;

            if (currentDay > chapterDays && i < actualLessonCount - 1)
            {
                currentDay = chapterDays;
            }
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
            var dueDate = lesson.LessonDay.AddDays(2);

            quizSchedules.Add(new QuizScheduleDto(
                LessonOrderIndex: lesson.OrderIndex,
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
    public int GetQuizzesPerLesson(ComplexityLevel complexity)
    {
        return complexity switch
        {
            ComplexityLevel.Beginner => 1,       // 1 quiz per lesson
            ComplexityLevel.Intermediate => 2,   // 1 quiz per lesson  
            ComplexityLevel.Advanced => 2,       // 2 quizzes per lesson 
            _ => 1
        };
    }
    private List<int> GetLessonDurations(int totalLessons, int availableDays, ComplexityLevel complexity)
    {
        var durations = new List<int>();
        var random = new Random(42);

        var durationWeights = complexity switch
        {
            ComplexityLevel.Beginner => new[] { (1, 60), (2, 30), (3, 10) }, // 60% 1-day, 30% 2-day, 10% 3-day
            ComplexityLevel.Intermediate => new[] { (1, 40), (2, 40), (3, 20) },
            ComplexityLevel.Advanced => new[] { (1, 30), (2, 40), (3, 30) },
            _ => new[] { (1, 60), (2, 30), (3, 10) }
        };

        for (int i = 0; i < totalLessons; i++)
        {
            var randomValue = random.Next(100);
            var cumulativeWeight = 0;
            var selectedDuration = 1;

            foreach (var (duration, weight) in durationWeights)
            {
                cumulativeWeight += weight;
                if (randomValue < cumulativeWeight)
                {
                    selectedDuration = duration;
                    break;
                }
            }

            durations.Add(selectedDuration);
        }

        var totalDurationNeeded = durations.Sum();
        if (totalDurationNeeded > availableDays)
        {
            var scaleFactor = (double)availableDays / totalDurationNeeded;
            for (int i = 0; i < durations.Count; i++)
            {
                durations[i] = Math.Max(1, (int)Math.Round(durations[i] * scaleFactor));
            }
        }

        return durations;
    }
}