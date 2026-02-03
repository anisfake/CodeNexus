namespace CodeNexus.Application.Features.Users.DTOs;

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record ChangePasswordResponse(
    string Message
);
