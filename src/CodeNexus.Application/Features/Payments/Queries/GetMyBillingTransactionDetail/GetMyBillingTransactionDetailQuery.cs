using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Payments.Queries.GetMyBillingTransactionDetail;

public record GetMyBillingTransactionDetailQuery(Guid PaymentTransactionId) : IRequest<Result<MyBillingTransactionDetailResponse>>;
