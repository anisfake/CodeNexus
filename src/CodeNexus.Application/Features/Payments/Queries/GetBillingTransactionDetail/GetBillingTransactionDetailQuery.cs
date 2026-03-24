using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Payments.Queries.GetBillingTransactionDetail;

public record GetBillingTransactionDetailQuery(Guid PaymentTransactionId)
    : IRequest<Result<BillingTransactionDetailResponse>>;

