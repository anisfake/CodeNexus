namespace CodeNexus.Application.Features.Auth.DTOs;

public record UserDto(
    Guid UserId,
    string Email,
    string Username,
    DateTime CreatedAt
);
