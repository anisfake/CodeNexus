namespace CodeNexus.Application.Features.Profile.DTOs;

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record ChangePasswordResponse(
    string Message
);
