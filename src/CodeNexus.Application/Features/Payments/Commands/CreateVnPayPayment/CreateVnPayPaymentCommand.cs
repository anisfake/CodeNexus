using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Payments.Commands.CreateVnPayPayment;

public record CreateVnPayPaymentCommand(
    Guid? SubscriptionPlanId,
    string? OrderInfo,
    string ReturnUrl,
    string IpAddress,
    string? IpnUrl = null) : IRequest<Result<VnPayCreatePaymentResponseDto>>;
