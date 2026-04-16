using FluentValidation;

namespace CodeNexus.Application.Features.SystemRuntimePolicies.Commands.UpdateSystemRuntimePolicy;

public class UpdateSystemRuntimePolicyCommandValidator : AbstractValidator<UpdateSystemRuntimePolicyCommand>
{
    public UpdateSystemRuntimePolicyCommandValidator()
    {
        RuleFor(x => x.PolicyKey)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.ConfigJson)
            .NotNull();

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}
