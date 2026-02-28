namespace CodeNexus.Application.Common.Interfaces;

public interface IGoalValidationService
{
    Task<bool> IsRelatedToProgrammingAsync(string goalTitle, CancellationToken cancellationToken = default);
}
