﻿using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.ForgotPassword;

public class ForgotPasswordCommanHandler : IRequestHandler<ForgotPasswordCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IOTPCacheService _otpCacheService;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommanHandler(
        IApplicationDbContext context, 
        IOTPCacheService otpCacheService, 
        IEmailService emailService)
    {
        _context = context;
        _otpCacheService = otpCacheService;
        _emailService = emailService;
    }

    public async Task<Result> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user == null)
        {
            return Result.Failure("USER_NOT_FOUND", "User not found.");
        }

        var result = await _otpCacheService.GenerateAndStoreOtpAsync(
            request.Email, 
            OtpPurpose.ResetPassword, 
            null, 
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result;
        }

        var otp = _otpCacheService.GetLastGeneratedOtp();
        await _emailService.SendOtpEmailAsync(request.Email, otp, cancellationToken);

        return Result.Success();
    }
}
