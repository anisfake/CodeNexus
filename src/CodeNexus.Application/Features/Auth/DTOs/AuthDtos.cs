using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Auth.DTOs;

public record RegisterRequest(
    string Email,
    string Username,
    string FirstName,
    string LastName,
    string Password
);
public record VerifyOtpRequest(
    string Email,
    string Otp
);
public record ResendOtpRequest(
    string Email
);
public record ForgotPasswordRequest(
    string Email
);
public record ResetPasswordRequest(
    string ResetToken,
    string NewPassword
);

public record AuthUserDto(
    Guid UserId,
    string Email,
    string Username,
    DateTime CreatedAt
);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string Email,
    string Username,
    DateTime? LastLogin,
    Guid? RoleId,
    string? RoleName,
    bool ShouldPromptDailyReminderTime
);

public record VerifyOtpResponse(
    OtpPurpose Purpose,
    string? ResetToken,
    string? Message
);

public record LogoutRequest(
    string? AccessToken = null,
    string? RefreshToken = null
);

public record RefreshTokenRequest(
    string RefreshToken
);
