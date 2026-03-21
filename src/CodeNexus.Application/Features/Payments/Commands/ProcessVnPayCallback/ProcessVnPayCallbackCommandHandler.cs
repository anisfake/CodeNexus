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
            return Result<VnPayCallbackResponseDto>.Failure("INVALID_REQUEST", "Missing vnp_TxnRef");
        }

        if (!_vnPayService.ValidateSignature(request.Parameters))
        {
            return Result<VnPayCallbackResponseDto>.Failure("INVALID_SIGNATURE", "Invalid VNPAY signature");
        }

        var payment = await _context.PaymentTransactions
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.TxnRef == txnRef, cancellationToken);

        if (payment == null)
        {
            return Result<VnPayCallbackResponseDto>.Failure("PAYMENT_NOT_FOUND", "Payment transaction not found");
        }

        if (!request.Parameters.TryGetValue("vnp_ResponseCode", out var responseCode))
        {
            responseCode = "99";
        }

        payment.ResponseCode = responseCode;
        payment.TransactionNo = request.Parameters.TryGetValue("vnp_TransactionNo", out var transNo) ? transNo : null;
        payment.BankCode = request.Parameters.TryGetValue("vnp_BankCode", out var bankCode) ? bankCode : null;

        if (TryParsePayDate(request.Parameters, out var paidAt))
        {
            payment.PaidAt = paidAt;
        }

        if (payment.Status == PaymentStatus.Success)
        {
            return Result<VnPayCallbackResponseDto>.Success(new VnPayCallbackResponseDto(
                payment.PaymentTransactionId,
                payment.Status,
                payment.ResponseCode ?? string.Empty,
                payment.Amount,
                payment.SubscriptionPlanId,
                payment.User.PlanExpiresAt));
        }

        if (responseCode == "00")
        {
            payment.Status = PaymentStatus.Success;
            if (!payment.SubscriptionPlanId.HasValue)
            {
                return Result<VnPayCallbackResponseDto>.Failure("SUBSCRIPTION_PLAN_NOT_FOUND", "Payment transaction is missing subscription plan.");
            }

            var subscriptionPlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(x => x.SubscriptionPlanId == payment.SubscriptionPlanId.Value && x.IsActive, cancellationToken);

            if (subscriptionPlan == null)
            {
                return Result<VnPayCallbackResponseDto>.Failure("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");
            }

            var now = DateTime.UtcNow;
            var effectiveEnd = payment.User.PlanExpiresAt.HasValue && payment.User.PlanExpiresAt.Value > now
                ? payment.User.PlanExpiresAt.Value.AddDays(subscriptionPlan.DurationDays)
                : now.AddDays(subscriptionPlan.DurationDays);

            payment.User.SubscriptionPlanId = subscriptionPlan.SubscriptionPlanId;
            payment.User.PlanExpiresAt = effectiveEnd;
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
            payment.User.PlanExpiresAt));
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
