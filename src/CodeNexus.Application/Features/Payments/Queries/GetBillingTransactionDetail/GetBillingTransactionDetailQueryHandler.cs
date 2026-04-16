using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Payments.Queries.GetBillingTransactionDetail;

public class GetBillingTransactionDetailQueryHandler
    : IRequestHandler<GetBillingTransactionDetailQuery, Result<BillingTransactionDetailResponse>>
{
    private readonly IApplicationDbContext _context;

    public GetBillingTransactionDetailQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<BillingTransactionDetailResponse>> Handle(GetBillingTransactionDetailQuery request, CancellationToken cancellationToken)
    {
        var payment = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.PaymentTransactionId == request.PaymentTransactionId, cancellationToken);

        if (payment == null)
        {
            return Result<BillingTransactionDetailResponse>.Failure("PAYMENT_NOT_FOUND", "Payment transaction not found.");
        }

        return Result<BillingTransactionDetailResponse>.Success(new BillingTransactionDetailResponse(
            payment.PaymentTransactionId,
            payment.UserId,
            payment.User.Username,
            payment.User.Email,
            payment.Amount,
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
