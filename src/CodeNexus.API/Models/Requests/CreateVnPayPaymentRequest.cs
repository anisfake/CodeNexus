namespace CodeNexus.API.Models.Requests;

public record CreateVnPayPaymentRequest(
    Guid? TokenPackageId,
    decimal? TopUpAmountVnd,
    string? OrderInfo,
    string ReturnUrl,
    Guid? MentorPackageId = null
);
