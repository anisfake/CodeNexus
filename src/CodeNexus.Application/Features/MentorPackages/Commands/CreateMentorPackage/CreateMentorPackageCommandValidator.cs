using FluentValidation;

namespace CodeNexus.Application.Features.MentorPackages.Commands.CreateMentorPackage;

public class CreateMentorPackageCommandValidator : AbstractValidator<CreateMentorPackageCommand>
{
    public CreateMentorPackageCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("NAME_REQUIRED")
            .MaximumLength(120).WithErrorCode("NAME_TOO_LONG");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithErrorCode("DESCRIPTION_TOO_LONG")
            .When(x => x.Description != null);

        RuleFor(x => x.PriceVnd)
            .GreaterThan(0).WithErrorCode("PRICE_INVALID")
            .WithMessage("PriceVnd must be greater than 0.");

        RuleFor(x => x.SharesFromMentorLimit)
            .GreaterThanOrEqualTo(-1).WithErrorCode("LIMIT_INVALID")
            .WithMessage("Limit must be -1 (unlimited) or a positive number.");

        RuleFor(x => x.ValidationRequestLimit)
            .GreaterThanOrEqualTo(-1).WithErrorCode("LIMIT_INVALID")
            .WithMessage("Limit must be -1 (unlimited) or a positive number.");

        RuleFor(x => x.TaskReviewLimit)
            .GreaterThanOrEqualTo(-1).WithErrorCode("LIMIT_INVALID")
            .WithMessage("Limit must be -1 (unlimited) or a positive number.");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithErrorCode("DISPLAY_ORDER_INVALID");
    }
}
