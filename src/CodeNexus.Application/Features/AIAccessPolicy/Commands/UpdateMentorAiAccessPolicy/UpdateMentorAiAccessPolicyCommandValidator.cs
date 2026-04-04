using FluentValidation;

namespace CodeNexus.Application.Features.AIAccessPolicy.Commands.UpdateMentorAiAccessPolicy;

public class UpdateMentorAiAccessPolicyCommandValidator : AbstractValidator<UpdateMentorAiAccessPolicyCommand>
{
    public UpdateMentorAiAccessPolicyCommandValidator()
    {
        RuleFor(x => x.MentorPaidRequestsMonthlyLimit)
            .GreaterThanOrEqualTo(0)
            .WithMessage("MentorPaidRequestsMonthlyLimit must be >= 0.");

        RuleFor(x => x.MentorDowngradeNotifyCooldownHours)
            .GreaterThan(0)
            .WithMessage("MentorDowngradeNotifyCooldownHours must be > 0.");
    }
}

