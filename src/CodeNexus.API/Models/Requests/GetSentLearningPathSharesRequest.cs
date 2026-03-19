using CodeNexus.Domain.Enums;

namespace CodeNexus.API.Models.Requests;

public record GetSentLearningPathSharesRequest(
    LearningPathShareStatus? Status = null,
    Guid? StudentId = null
);
