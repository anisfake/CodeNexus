using CodeNexus.Domain.Entities;

namespace CodeNexus.Application.Common.Interfaces;

public interface ISubscriptionAccessService
{
    Task<SubscriptionPlan> GetEffectivePlanAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanUsePersonalGoalsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CanUsePaidModelsAsync(Guid userId, CancellationToken cancellationToken = default);
}
