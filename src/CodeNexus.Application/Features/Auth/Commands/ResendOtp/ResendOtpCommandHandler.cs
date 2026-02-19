using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Auth.Commands.ResendOtp;

public class ResendOtpCommandHandler : IRequestHandler<ResendOtpCommand, Result>
{
    private readonly IEmailService _emailService;
    private readonly IOTPCacheService _otpCacheService;

    public ResendOtpCommandHandler(
        IEmailService emailService,
        IOTPCacheService otpCacheService)
    {
        _emailService = emailService;
        _otpCacheService = otpCacheService;
    }

    public async Task<Result> Handle(ResendOtpCommand request, CancellationToken cancellationToken)
    {
        var result = await _otpCacheService.ResendOtpAsync(request.Email, cancellationToken);

        if (!result.IsSuccess)
        {
            return result;
        }

        var otp = _otpCacheService.GetLastGeneratedOtp();
        await _emailService.SendOtpEmailAsync(request.Email, otp, cancellationToken);

        return Result.Success();
    }
}
