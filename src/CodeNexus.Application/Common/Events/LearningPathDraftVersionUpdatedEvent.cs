using MediatR;

namespace CodeNexus.Application.Common.Events;

public record LearningPathDraftVersionUpdatedEvent(
    Guid PathId,
    Guid MentorId,
    string MentorUserName,
    decimal CurrentVersion,
    DateTime OccurredAt
) : INotification;