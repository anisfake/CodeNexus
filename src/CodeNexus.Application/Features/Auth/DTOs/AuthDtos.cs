namespace CodeNexus.Application.Features.Auth.DTOs;

public record UserDto(
    Guid UserId,
    string Email,
    string Username,
    DateTime CreatedAt
);

public class VerifyOtpResponse
{
    public string Purpose { get; set; } = string.Empty;
    public string? ResetToken { get; set; }
    public string? Message { get; set; }
}
