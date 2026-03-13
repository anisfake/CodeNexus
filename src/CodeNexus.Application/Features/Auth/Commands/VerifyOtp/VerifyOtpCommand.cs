using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Auth.Commands.VerifyOtp;

public record VerifyOtpCommand(
    string Email,
    string Otp
) : IRequest<Result<VerifyOtpResponse>>;
