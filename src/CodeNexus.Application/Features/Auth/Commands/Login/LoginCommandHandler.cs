using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly IOTPService _otpService;
    private readonly ITokenService _tokenService;

    public LoginCommandHandler(IApplicationDbContext context, IOTPService otpService, ITokenService tokenService)
    {
        _context = context;
        _otpService = otpService;
        _tokenService = tokenService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == identifier || u.Username == identifier, cancellationToken);

        if (user == null)
            return Result<LoginResponse>.Failure("INVALID_CREDENTIALS", "Invalid credentials");

        if (!_otpService.VerifyOtp(request.Password, user.PasswordHash))
            return Result<LoginResponse>.Failure("INVALID_CREDENTIALS", "Invalid credentials");

        var accessToken = _tokenService.GenerateAccessToken(user);

        return Result<LoginResponse>.Success(new LoginResponse(
            accessToken,
            user.UserId,
            user.Email,
            user.Username
        ));
    }
}
