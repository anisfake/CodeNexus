using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Payments.Queries.GetBillingSummary;

public record GetBillingSummaryQuery(
    DateTime? FromUtc = null,
    DateTime? ToUtc = null) : IRequest<Result<BillingSummaryResponse>>;

