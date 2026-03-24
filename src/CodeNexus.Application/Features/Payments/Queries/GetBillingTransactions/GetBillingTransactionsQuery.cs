using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Payments.Queries.GetBillingTransactions;

public record GetBillingTransactionsQuery : IRequest<Result<PaginationDto<BillingTransactionResponse>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public DateTime? FromUtc { get; init; }
    public DateTime? ToUtc { get; init; }
    public PaymentStatus? Status { get; init; }
    public Guid? UserId { get; init; }
    public Guid? SubscriptionPlanId { get; init; }
    public string? Provider { get; init; }
    public string? Search { get; init; }
}

