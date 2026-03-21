using CodeNexus.Application.Features.Subscriptions.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Subscriptions.Queries.GetCurrentSubscription;

public record GetCurrentSubscriptionQuery() : IRequest<CurrentSubscriptionDto>;
