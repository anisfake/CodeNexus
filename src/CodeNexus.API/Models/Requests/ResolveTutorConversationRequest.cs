namespace CodeNexus.API.Models.Requests;

public record ResolveTutorConversationRequest(
    Guid? LearningPathId,
    Guid? ChapterId,
    Guid? LessonId,
    bool CreateIfMissing = true
);
