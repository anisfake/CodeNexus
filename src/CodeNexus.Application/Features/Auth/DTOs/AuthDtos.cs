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
public record UserDto(
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
    Guid? RoleId,
    string? RoleName
);

public class VerifyOtpResponse
{
    public string Purpose { get; set; } = string.Empty;
    public string? ResetToken { get; set; }
    public string? Message { get; set; }
}
