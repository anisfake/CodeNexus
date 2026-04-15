using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Payments.Commands.ProcessVnPayCallback;

public class ProcessVnPayCallbackCommandHandler
    : IRequestHandler<ProcessVnPayCallbackCommand, Result<VnPayCallbackResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IVnPayService _vnPayService;

    public ProcessVnPayCallbackCommandHandler(
        IApplicationDbContext context,
        IVnPayService vnPayService)
    {
        _context = context;
        _vnPayService = vnPayService;
    }

    public async Task<Result<VnPayCallbackResponseDto>> Handle(ProcessVnPayCallbackCommand request, CancellationToken cancellationToken)
    {
        if (!request.Parameters.TryGetValue("vnp_TxnRef", out var txnRef) || string.IsNullOrWhiteSpace(txnRef))
        {
            return Result<VnPayCallbackResponseDto>.Failure("INVALID_REQUEST", "Invalid request.");
        }

        if (!_vnPayService.ValidateSignature(request.Parameters))
        {
            return Result<VnPayCallbackResponseDto>.Failure("INVALID_SIGNATURE", "Invalid VNPAY signature");
        }

        if (_context is DbContext dbContext)
        {
            await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var transactionalResult = await ProcessInternalAsync(txnRef, request.Parameters, cancellationToken);
            if (transactionalResult.IsSuccess)
            {
                await tx.CommitAsync(cancellationToken);
            }
            else
            {
                await tx.RollbackAsync(cancellationToken);
            }

            return transactionalResult;
        }

        return await ProcessInternalAsync(txnRef, request.Parameters, cancellationToken);
    }

    private async Task<Result<VnPayCallbackResponseDto>> ProcessInternalAsync(
        string txnRef,
        IDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        var payment = await _context.PaymentTransactions
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.TxnRef == txnRef, cancellationToken);

        if (payment == null)
        {
            return Result<VnPayCallbackResponseDto>.Failure("PAYMENT_NOT_FOUND", "Payment transaction not found.");
        }

        if (!parameters.TryGetValue("vnp_ResponseCode", out var responseCode))
        {
            responseCode = "99";
        }

        payment.ResponseCode = responseCode;
        payment.TransactionNo = parameters.TryGetValue("vnp_TransactionNo", out var transNo) ? transNo : null;
        payment.BankCode = parameters.TryGetValue("vnp_BankCode", out var bankCode) ? bankCode : null;

        if (TryParsePayDate(parameters, out var paidAt))
        {
            payment.PaidAt = paidAt;
        }

        if (payment.Status == PaymentStatus.Success)
        {
            payment.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<VnPayCallbackResponseDto>.Success(new VnPayCallbackResponseDto(
                payment.PaymentTransactionId,
                payment.Status,
                payment.ResponseCode ?? string.Empty,
                payment.Amount,
                payment.SubscriptionPlanId,
                payment.User.PlanExpiresAt,
                payment.User.BalanceVnd,
                payment.CreditedAmountVnd));
        }

        if (responseCode == "00")
        {
            payment.Status = PaymentStatus.Success;
            var creditedAmount = payment.CreditedAmountVnd > 0m ? payment.CreditedAmountVnd : payment.Amount;
            payment.User.BalanceVnd += creditedAmount;
        }
        else if (responseCode == "24")
        {
            payment.Status = PaymentStatus.Canceled;
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
        }

        payment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<VnPayCallbackResponseDto>.Success(new VnPayCallbackResponseDto(
            payment.PaymentTransactionId,
            payment.Status,
            payment.ResponseCode ?? string.Empty,
            payment.Amount,
            payment.SubscriptionPlanId,
            payment.User.PlanExpiresAt,
            payment.User.BalanceVnd,
            payment.CreditedAmountVnd));
    }

    private static bool TryParsePayDate(IDictionary<string, string> parameters, out DateTime? paidAt)
    {
        paidAt = null;
        if (!parameters.TryGetValue("vnp_PayDate", out var value) || string.IsNullOrWhiteSpace(value))
            return false;

        if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", null, System.Globalization.DateTimeStyles.None, out var parsed))
        {
            paidAt = parsed;
            return true;
        }

        return false;
    }
}
