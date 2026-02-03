namespace CodeNexus.Application.Features.Auth.DTOs;

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
