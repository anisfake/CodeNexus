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
    DateTime? DateOfBirth,
    string? Phone,
    string? Address
);

public record UpdateProfileRequest(
    string? FirstName,
    string? LastName,
    string? Bio,
    DateTime? DateOfBirth,
    string? Phone,
    string? Address
);
