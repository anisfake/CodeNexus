using FluentValidation;

namespace CodeNexus.Application.Features.Payments.Commands.CreateVnPayPayment;

public class CreateVnPayPaymentCommandValidator : AbstractValidator<CreateVnPayPaymentCommand>
{
    public CreateVnPayPaymentCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.TokenPackageId.HasValue || x.TopUpAmountVnd.HasValue || x.MentorPackageId.HasValue)
            .WithMessage("One of TokenPackageId, MentorPackageId, or TopUpAmountVnd is required.")
            .Must(x =>
            {
                var count = (x.TokenPackageId.HasValue ? 1 : 0)
                          + (x.TopUpAmountVnd.HasValue ? 1 : 0)
                          + (x.MentorPackageId.HasValue ? 1 : 0);
                return count <= 1;
            })
            .WithMessage("Only one of TokenPackageId, MentorPackageId, or TopUpAmountVnd may be provided.");

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
