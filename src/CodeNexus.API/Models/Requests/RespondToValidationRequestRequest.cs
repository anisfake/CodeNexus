namespace CodeNexus.API.Models.Requests;

public record RespondToValidationRequestRequest(
    string? Feedback,
    bool Accept
);
