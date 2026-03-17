namespace CodeNexus.API.Models.Requests;

public record SendTutorMessageRequest(
    Guid? ConversationId,
    Guid? LearningPathId,
    Guid? ChapterId,
    Guid? LessonId,
    string Message
);
