using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Payments.Queries.GetBillingSummary;

public class GetBillingSummaryQueryHandler
    : IRequestHandler<GetBillingSummaryQuery, Result<BillingSummaryResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetBillingSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BillingSummaryResponse>> Handle(GetBillingSummaryQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PaymentTransactions.AsNoTracking().AsQueryable();

        if (request.FromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= request.ToUtc.Value);
        }

        var totalTransactions = await query.CountAsync(cancellationToken);
        var pending = await query.CountAsync(x => x.Status == PaymentStatus.Pending, cancellationToken);
        var success = await query.CountAsync(x => x.Status == PaymentStatus.Success, cancellationToken);
        var failed = await query.CountAsync(x => x.Status == PaymentStatus.Failed, cancellationToken);
        var canceled = await query.CountAsync(x => x.Status == PaymentStatus.Canceled, cancellationToken);
        var revenue = await query
            .Where(x => x.Status == PaymentStatus.Success)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var dailyRaw = await query
            .Where(x => x.Status == PaymentStatus.Success)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                Transactions = g.Count(),
                RevenueVnd = g.Sum(x => x.Amount)
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        var daily = dailyRaw
            .Select(x => new BillingSummaryItemResponse(
                DateOnly.FromDateTime(x.Date),
                x.Transactions,
                x.Transactions,
                x.RevenueVnd))
            .ToList();

        return Result<BillingSummaryResponse>.Success(new BillingSummaryResponse(
            request.FromUtc,
            request.ToUtc,
            totalTransactions,
            pending,
            success,
            failed,
            canceled,
            revenue,
            daily));
    }
}
