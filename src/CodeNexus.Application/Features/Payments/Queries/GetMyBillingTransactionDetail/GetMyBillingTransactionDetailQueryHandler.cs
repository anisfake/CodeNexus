using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Payments.Queries.GetMyBillingTransactionDetail;

public class GetMyBillingTransactionDetailQueryHandler
    : IRequestHandler<GetMyBillingTransactionDetailQuery, Result<MyBillingTransactionDetailResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyBillingTransactionDetailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<MyBillingTransactionDetailResponse>> Handle(GetMyBillingTransactionDetailQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var payment = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(x => x.TokenPackage)
            .FirstOrDefaultAsync(
                x => x.PaymentTransactionId == request.PaymentTransactionId && x.UserId == userId,
                cancellationToken);

        if (payment == null)
        {
            return Result<MyBillingTransactionDetailResponse>.Failure("PAYMENT_NOT_FOUND", "Payment transaction not found.");
        }

        return Result<MyBillingTransactionDetailResponse>.Success(new MyBillingTransactionDetailResponse(
            payment.PaymentTransactionId,
            payment.TokenPackageId,
            payment.TokenPackage?.Name,
            payment.Amount,
            payment.CreditedTokens,
            payment.Provider,
            payment.TxnRef,
            payment.OrderInfo,
            payment.Status,
            payment.ResponseCode,
            payment.TransactionNo,
            payment.BankCode,
            payment.PaidAt,
            payment.CreatedAt,
            payment.UpdatedAt));
    }
}

