namespace CodeNexus.Application.Common.Interfaces;

public interface IDailyReminderTimeInferenceService
{
    Task<TimeSpan> InferDailyReminderTimeAsync(Guid userId, CancellationToken cancellationToken);
}
