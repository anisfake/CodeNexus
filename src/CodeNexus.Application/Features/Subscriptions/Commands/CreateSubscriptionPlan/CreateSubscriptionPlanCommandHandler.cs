using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subscriptions.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subscriptions.Commands.CreateSubscriptionPlan;

public class CreateSubscriptionPlanCommandHandler : IRequestHandler<CreateSubscriptionPlanCommand, Result<SubscriptionPlanDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateSubscriptionPlanCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SubscriptionPlanDto>> Handle(CreateSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.SubscriptionPlans
            .AsNoTracking()
            .AnyAsync(x => x.PlanType == request.PlanType, cancellationToken);

        if (exists)
        {
            return Result<SubscriptionPlanDto>.Failure("SUBSCRIPTION_PLAN_EXISTS", "Subscription plan type already exists.");
        }

        var plan = new SubscriptionPlan
        {
            SubscriptionPlanId = NewId.NextGuid(),
            PlanType = request.PlanType,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PriceVnd = request.PriceVnd,
            DurationDays = request.DurationDays,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder
        };

        await _context.SubscriptionPlans.AddAsync(plan, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<SubscriptionPlanDto>.Success(new SubscriptionPlanDto(
            plan.SubscriptionPlanId,
            plan.PlanType,
            plan.Name,
            plan.Description,
            plan.PriceVnd,
            plan.DurationDays,
            plan.IsActive,
            plan.DisplayOrder));
    }
}
