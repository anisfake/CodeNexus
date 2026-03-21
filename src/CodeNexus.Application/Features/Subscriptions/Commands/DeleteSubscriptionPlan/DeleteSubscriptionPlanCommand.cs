using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Subscriptions.Commands.DeleteSubscriptionPlan;

public record DeleteSubscriptionPlanCommand(Guid SubscriptionPlanId) : IRequest<Result<string>>;
