using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Payments.Commands.CreateVnPayPayment;

public class CreateVnPayPaymentCommandHandler
    : IRequestHandler<CreateVnPayPaymentCommand, Result<VnPayCreatePaymentResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVnPayService _vnPayService;

    public CreateVnPayPaymentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IVnPayService vnPayService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _vnPayService = vnPayService;
    }

    public async Task<Result<VnPayCreatePaymentResponseDto>> Handle(CreateVnPayPaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var userExists = await _context.Users
            .AsNoTracking()
            .AnyAsync(u => u.UserId == userId, cancellationToken);

        if (!userExists)
        {
            return Result<VnPayCreatePaymentResponseDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!request.SubscriptionPlanId.HasValue)
        {
            return Result<VnPayCreatePaymentResponseDto>.Failure("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");
        }

        var subscriptionPlan = await _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubscriptionPlanId == request.SubscriptionPlanId.Value && x.IsActive, cancellationToken);

        if (subscriptionPlan == null)
        {
            return Result<VnPayCreatePaymentResponseDto>.Failure("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");
        }

        if (subscriptionPlan.PriceVnd <= 0)
        {
            return Result<VnPayCreatePaymentResponseDto>.Failure("INVALID_SUBSCRIPTION_PLAN", "Free plan cannot be purchased through payment.");
        }

        var amount = subscriptionPlan.PriceVnd;
        var defaultOrderInfo = $"Purchase {subscriptionPlan.Name} plan";

        var txnRef = NewId.NextGuid().ToString("N");
        var orderInfo = string.IsNullOrWhiteSpace(request.OrderInfo)
            ? defaultOrderInfo
            : request.OrderInfo.Trim();

        var payment = new PaymentTransaction
        {
            PaymentTransactionId = NewId.NextGuid(),
            UserId = userId,
            SubscriptionPlanId = subscriptionPlan.SubscriptionPlanId,
            Amount = amount,
            Provider = "VNPAY",
            TxnRef = txnRef,
            OrderInfo = orderInfo,
            Status = Domain.Enums.PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _context.PaymentTransactions.AddAsync(payment, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        var ipAddress = string.IsNullOrWhiteSpace(request.IpAddress) ? "127.0.0.1" : request.IpAddress;
        var paymentUrl = _vnPayService.CreatePaymentUrl(
            txnRef,
            amount,
            orderInfo,
            ipAddress,
            request.ReturnUrl,
            request.IpnUrl);

        return Result<VnPayCreatePaymentResponseDto>.Success(new VnPayCreatePaymentResponseDto(
            payment.PaymentTransactionId,
            txnRef,
            paymentUrl));
    }
}
