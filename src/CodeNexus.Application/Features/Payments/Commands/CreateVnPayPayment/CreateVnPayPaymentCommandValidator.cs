using FluentValidation;

namespace CodeNexus.Application.Features.Payments.Commands.CreateVnPayPayment;

public class CreateVnPayPaymentCommandValidator : AbstractValidator<CreateVnPayPaymentCommand>
{
    public CreateVnPayPaymentCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.SubscriptionPlanId.HasValue)
            .WithMessage("SubscriptionPlanId is required.");

        RuleFor(x => x.IpAddress)
            .NotEmpty()
            .WithMessage("IpAddress is required.");

        RuleFor(x => x.ReturnUrl)
            .NotEmpty()
            .WithMessage("ReturnUrl is required.");
    }
}
