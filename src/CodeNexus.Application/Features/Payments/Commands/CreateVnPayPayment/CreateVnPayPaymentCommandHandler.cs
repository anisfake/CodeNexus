using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using CodeNexus.Application.Features.SystemRuntimePolicies;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Payments.Commands.CreateVnPayPayment;

public class CreateVnPayPaymentCommandHandler
    : IRequestHandler<CreateVnPayPaymentCommand, Result<VnPayCreatePaymentResponseDto>>
{
    private const decimal DefaultCustomTopUpVndPerToken = 100m;
    private const string TokenPricingPolicyKey = "token_pricing_policy";
    private const string VndPerTokenConfigKey = "vndPerToken";

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
        string defaultOrderInfo;

        if (request.TokenPackageId.HasValue)
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
            if (!request.TopUpAmountVnd.HasValue)
            {
                return Result<VnPayCreatePaymentResponseDto>.Failure("INVALID_TOPUP_AMOUNT", "Top-up amount must be provided.");
            }

            amount = Math.Round(request.TopUpAmountVnd.Value, 0, MidpointRounding.AwayFromZero);
            if (amount <= 0)
            {
                return Result<VnPayCreatePaymentResponseDto>.Failure("INVALID_TOPUP_AMOUNT", "Top-up amount must be greater than 0.");
            }

            if (amount < 10000m)
            {
                return Result<VnPayCreatePaymentResponseDto>.Failure("INVALID_TOPUP_AMOUNT", "Minimum top-up amount is 10,000 VND.");
            }

            if (amount > 50000000m)
            {
                return Result<VnPayCreatePaymentResponseDto>.Failure("INVALID_TOPUP_AMOUNT", "Top-up amount is too large.");
            }

            var vndPerToken = await ResolveCustomTopUpVndPerTokenAsync(cancellationToken);
            creditedTokens = Math.Floor(amount / vndPerToken);
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
            SubscriptionPlanId = null,
            TokenPackageId = tokenPackageId,
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

    private async Task<decimal> ResolveCustomTopUpVndPerTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            var policy = await _context.SystemRuntimePolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PolicyKey == TokenPricingPolicyKey && x.IsActive, cancellationToken);

            if (policy == null)
            {
                return DefaultCustomTopUpVndPerToken;
            }

            var config = SystemRuntimePolicyJsonHelper.ParseConfigJson(policy.ConfigJson);
            if (!config.TryGetValue(VndPerTokenConfigKey, out var rawValue))
            {
                return DefaultCustomTopUpVndPerToken;
            }

            var parsed = ParsePositiveDecimal(rawValue);
            return parsed > 0m ? parsed : DefaultCustomTopUpVndPerToken;
        }
        catch
        {
            return DefaultCustomTopUpVndPerToken;
        }
    }

    private static decimal ParsePositiveDecimal(object? rawValue)
    {
        return rawValue switch
        {
            decimal d when d > 0m => d,
            double d when d > 0d => (decimal)d,
            float f when f > 0f => (decimal)f,
            int i when i > 0 => i,
            long l when l > 0 => l,
            string s when decimal.TryParse(s, out var parsed) && parsed > 0m => parsed,
            _ => 0m
        };
    }
}

