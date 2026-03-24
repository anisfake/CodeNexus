namespace CodeNexus.API.Models.Requests;

public record CreateVnPayPaymentRequest(
    Guid? SubscriptionPlanId,
    string? OrderInfo,
    string ReturnUrl
);
