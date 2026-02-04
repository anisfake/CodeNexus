namespace CodeNexus.Application.Features.Users.DTOs;

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record ChangePasswordResponse(
    string Message
);

public record UserProfileRespone(
    string Email,
    string FirstName,
    string LastName,
    string Bio,
    string Username,
    string? AvatarUrl,
    string? DateOfBirth,
    string? Phone,
    string? Address
);
