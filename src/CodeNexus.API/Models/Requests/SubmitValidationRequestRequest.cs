namespace CodeNexus.API.Models.Requests;

public record SubmitValidationRequestRequest(
    Guid PathId,
    Guid MentorId,
    string? StudentNote
);
