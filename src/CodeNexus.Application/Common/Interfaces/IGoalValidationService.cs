namespace CodeNexus.Application.Common.Interfaces;

public interface IGoalValidationService
{
    Task<bool> IsRelatedToProgrammingAsync(string goalTitle, CancellationToken cancellationToken = default);
    Task<bool> IsGoalRelevantToSubjectAsync(
        string goalTitle,
        string? goalDescription,
        string subjectName,
        string? subjectDescription,
        CancellationToken cancellationToken = default);
}
