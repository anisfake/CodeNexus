using CodeNexus.Application.Features.Users.Queries.GetAllUsers;

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
    string? FirstName,
    string? LastName,
    string? Bio,
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

public record UserRespone(
    Guid UserId,
    string Username,
    string Email,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    string? Bio,
    string? Phone,
    string? Address,
    DateTime? DateOfBirth,
    DateTime? LastLogin,
    string? Status,
    string? RoleName,
    DateTime CreatedAt
);
public record GetAllUsersRequest(
    int PageNumber = 1,
    int PageSize = 10,
    string? Role = null,
    string? SearchTerm = null,
    UserSortBy SortBy = UserSortBy.CreatedAt,
    bool SortDescending = true
);