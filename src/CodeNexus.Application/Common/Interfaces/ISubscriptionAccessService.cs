namespace CodeNexus.Application.Common.Interfaces;

public interface ISubscriptionAccessService
{
    Task<bool> CanUsePersonalGoalsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanUsePaidModelsAsync(Guid userId, CancellationToken cancellationToken = default);
}
