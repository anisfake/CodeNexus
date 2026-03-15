namespace CodeNexus.Application.Common.Models;

public record GoalMatchCandidate(
    Guid GoalId,
    string Title,
    string? Description
);

public record GoalMatchResult(
    Guid? GoalId,
    decimal? Confidence
);
