namespace CodeNexus.Application.Features.LearningPaths.DTOs;

public class QuizDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class LessonDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<QuizDto> Quizzes { get; set; } = new();
}

public class TaskDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class ChapterDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int OrderIndex { get; set; }
    public List<LessonDto> Lessons { get; set; } = new();
    public List<TaskDto> Tasks { get; set; } = new();
}

public class LearningPathSkeletonDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ChapterDto> Chapters { get; set; } = new();
}

public class CreateLearningPathResponse
{
    public Guid PathId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ChapterCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public CreateLearningPathResponse(Guid pathId, string title, string description, int chapterCount, DateTime createdAt)
    {
        PathId = pathId;
        Title = title;
        Description = description;
        ChapterCount = chapterCount;
        CreatedAt = createdAt;
    }
}
