using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Payments.Queries.GetMyBillingTransactions;

public class GetMyBillingTransactionsQueryHandler
    : IRequestHandler<GetMyBillingTransactionsQuery, Result<PaginationDto<MyBillingTransactionResponse>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyBillingTransactionsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginationDto<MyBillingTransactionResponse>>> Handle(GetMyBillingTransactionsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        var query = _context.PaymentTransactions
            .AsNoTracking()
            .Include(x => x.TokenPackage)
            .Where(x => x.UserId == userId)
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
                || (x.TransactionNo != null && x.TransactionNo.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new MyBillingTransactionResponse(
                x.PaymentTransactionId,
                x.TokenPackageId,
                x.TokenPackage != null ? x.TokenPackage.Name : null,
                x.Amount,
                x.CreditedAmountVnd,
                x.Provider,
                x.TxnRef,
                x.OrderInfo,
                x.Status,
                x.ResponseCode,
                x.TransactionNo,
                x.BankCode,
                x.PaidAt,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<PaginationDto<MyBillingTransactionResponse>>.Success(new PaginationDto<MyBillingTransactionResponse>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }
}
