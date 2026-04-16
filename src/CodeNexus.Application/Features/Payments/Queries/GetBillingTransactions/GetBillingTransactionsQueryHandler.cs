using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Payments.Queries.GetBillingTransactions;

public class GetBillingTransactionsQueryHandler
    : IRequestHandler<GetBillingTransactionsQuery, Result<PaginationDto<BillingTransactionResponse>>>
{
    private readonly IApplicationDbContext _context;

    public GetBillingTransactionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginationDto<BillingTransactionResponse>>> Handle(GetBillingTransactionsQuery request, CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        var query = _context.PaymentTransactions
            .AsNoTracking()
            .Include(x => x.User)
            .AsQueryable();

        if (request.FromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= request.ToUtc.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }

        if (request.UserId.HasValue)
        {
            query = query.Where(x => x.UserId == request.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Provider))
        {
            var provider = request.Provider.Trim();
            query = query.Where(x => x.Provider.Contains(provider));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x =>
                x.TxnRef.Contains(search)
                || x.OrderInfo.Contains(search)
                || (x.TransactionNo != null && x.TransactionNo.Contains(search))
                || (x.User.Username != null && x.User.Username.Contains(search))
                || (x.User.Email != null && x.User.Email.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new BillingTransactionResponse(
                x.PaymentTransactionId,
                x.UserId,
                x.User.Username,
                x.User.Email,
                x.Amount,
                x.Provider,
                x.TxnRef,
                x.Status,
                x.ResponseCode,
                x.TransactionNo,
                x.BankCode,
                x.OrderInfo,
                x.PaidAt,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<PaginationDto<BillingTransactionResponse>>.Success(new PaginationDto<BillingTransactionResponse>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }
}
