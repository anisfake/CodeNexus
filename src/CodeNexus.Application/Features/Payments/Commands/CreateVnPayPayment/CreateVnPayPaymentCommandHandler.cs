using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments;
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

        decimal amount;
        decimal creditedTokens;
        Guid? tokenPackageId = null;
        Guid? mentorPackageId = null;
        string defaultOrderInfo;

        if (request.MentorPackageId.HasValue)
        {
            var mentorPackage = await _context.MentorPackages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.MentorPackageId == request.MentorPackageId.Value && x.IsActive, cancellationToken);

            if (mentorPackage == null)
                return Result<VnPayCreatePaymentResponseDto>.Failure("MENTOR_PACKAGE_NOT_FOUND", "Mentor package not found.");

            amount = mentorPackage.PriceVnd;
            creditedTokens = 0;
            mentorPackageId = mentorPackage.MentorPackageId;
            defaultOrderInfo = $"Buy mentor package: {mentorPackage.Name}";
        }
        else if (request.TokenPackageId.HasValue)
        {
            var tokenPackage = await _context.TokenPackages
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TokenPackageId == request.TokenPackageId.Value && x.IsActive, cancellationToken);

            if (tokenPackage == null)
            {
                return Result<VnPayCreatePaymentResponseDto>.Failure("TOKEN_PACKAGE_NOT_FOUND", "Token package not found.");
            }

            amount = tokenPackage.PriceVnd;
            creditedTokens = tokenPackage.CreditedTokens;
            tokenPackageId = tokenPackage.TokenPackageId;
            defaultOrderInfo = $"Buy package {tokenPackage.Name}";
        }
        else
        {
            var pricingPolicy = await TokenPricingPolicyResolver.ResolveAsync(_context, cancellationToken);

            if (!request.TopUpAmountVnd.HasValue)
            {
                return Result<VnPayCreatePaymentResponseDto>.Failure("INVALID_TOPUP_AMOUNT", "Top-up amount must be provided.");
            }

            amount = Math.Round(request.TopUpAmountVnd.Value, 0, MidpointRounding.AwayFromZero);
            if (amount <= 0)
            {
                return Result<VnPayCreatePaymentResponseDto>.Failure("INVALID_TOPUP_AMOUNT", "Top-up amount must be greater than 0.");
            }

            if (amount < pricingPolicy.MinCustomTopUpVnd)
            {
                return Result<VnPayCreatePaymentResponseDto>.Failure(
                    "INVALID_TOPUP_AMOUNT",
                    $"Minimum top-up amount is {pricingPolicy.MinCustomTopUpVnd:N0} VND.");
            }

            if (amount > pricingPolicy.MaxCustomTopUpVnd)
            {
                return Result<VnPayCreatePaymentResponseDto>.Failure("INVALID_TOPUP_AMOUNT", "Top-up amount is too large.");
            }

            creditedTokens = Math.Floor(amount / pricingPolicy.VndPerToken);
            if (creditedTokens <= 0m)
            {
                return Result<VnPayCreatePaymentResponseDto>.Failure("INVALID_TOPUP_AMOUNT", "Top-up amount is too small for current token rate.");
            }

            defaultOrderInfo = $"Top-up {amount:N0} VND ({creditedTokens:N0} tokens)";
        }

        var txnRef = NewId.NextGuid().ToString("N");
        var orderInfo = string.IsNullOrWhiteSpace(request.OrderInfo)
            ? defaultOrderInfo
            : request.OrderInfo.Trim();

        var payment = new PaymentTransaction
        {
            PaymentTransactionId = NewId.NextGuid(),
            UserId = userId,
            TokenPackageId = tokenPackageId,
            MentorPackageId = mentorPackageId,
            Amount = amount,
            CreditedTokens = creditedTokens,
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

