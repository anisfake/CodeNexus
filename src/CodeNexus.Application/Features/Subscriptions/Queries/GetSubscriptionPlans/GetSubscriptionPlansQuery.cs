using CodeNexus.Application.Features.Subscriptions.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Subscriptions.Queries.GetSubscriptionPlans;

public record GetSubscriptionPlansQuery(bool ActiveOnly = true) : IRequest<List<SubscriptionPlanDto>>;
