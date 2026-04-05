namespace CodeNexus.Application.Features.LearningPathShares.DTOs;

public record LearningPathShareUpdateChangeSummaryDto(
    int AddedChapterCount,
    int RemovedChapterCount,
    int UpdatedChapterCount,
    int AddedLessonCount,
    int RemovedLessonCount,
    int UpdatedLessonCount,
    int AddedTaskCount,
    int RemovedTaskCount,
    int UpdatedTaskCount,
    List<string> AddedChapters,
    List<string> RemovedChapters,
    List<string> UpdatedChapters,
    List<string> AddedLessons,
    List<string> RemovedLessons,
    List<string> UpdatedLessons,
    List<string> AddedTasks,
    List<string> RemovedTasks,
    List<string> UpdatedTasks
);