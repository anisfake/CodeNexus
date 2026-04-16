using FluentValidation;

namespace CodeNexus.Application.Features.Payments.Commands.CreateVnPayPayment;

public class CreateVnPayPaymentCommandValidator : AbstractValidator<CreateVnPayPaymentCommand>
{
    public CreateVnPayPaymentCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.TokenPackageId.HasValue || x.TopUpAmountVnd.HasValue)
            .WithMessage("TokenPackageId or TopUpAmountVnd is required.");

        RuleFor(x => x.TopUpAmountVnd)
            .GreaterThan(0)
            .When(x => x.TopUpAmountVnd.HasValue)
            .WithMessage("TopUpAmountVnd must be greater than 0.");

        RuleFor(x => x.IpAddress)
            .NotEmpty()
            .WithMessage("IpAddress is required.");

        RuleFor(x => x.ReturnUrl)
            .NotEmpty()
            .WithMessage("ReturnUrl is required.");
    }
}
