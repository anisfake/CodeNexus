using FluentValidation;

namespace CodeNexus.Application.Features.TutorChat.Queries.ResolveTutorConversation;

public class ResolveTutorConversationQueryValidator : AbstractValidator<ResolveTutorConversationQuery>
{
    public ResolveTutorConversationQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.LearningPathId.HasValue || x.ChapterId.HasValue || x.LessonId.HasValue)
            .WithMessage("Learning path, chapter, or lesson id is required.");
    }
}
