using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IOTPCacheService _otpCacheService;
    private readonly IPasswordService _passwordService;

    public RegisterCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IOTPCacheService otpCacheService,
        IPasswordService passwordService)
    {
        _context = context;
        _emailService = emailService;
        _otpCacheService = otpCacheService;
        _passwordService = passwordService;
    }

    public async Task<Result> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email == request.Email, cancellationToken);

        if (emailExists)
        {
            return Result.Failure("EMAIL_EXISTS", "Email already registered");
        }

        var usernameExists = await _context.Users
            .AnyAsync(u => u.Username == request.Username, cancellationToken);

        if (usernameExists)
        {
            return Result.Failure("USERNAME_EXISTS", "Username already taken");
        }

        var passwordHash = _passwordService.HashPassword(request.Password);
        var registrationData = $"{request.Username}|{passwordHash}|{request.FirstName}|{request.LastName}";

        var result = await _otpCacheService.GenerateAndStoreOtpAsync(
            request.Email, 
            OtpPurpose.Register, 
            registrationData, 
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
