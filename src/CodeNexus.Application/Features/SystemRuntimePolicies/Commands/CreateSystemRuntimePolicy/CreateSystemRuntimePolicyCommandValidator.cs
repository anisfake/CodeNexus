using FluentValidation;

namespace CodeNexus.Application.Features.SystemRuntimePolicies.Commands.CreateSystemRuntimePolicy;

public class CreateSystemRuntimePolicyCommandValidator : AbstractValidator<CreateSystemRuntimePolicyCommand>
{
    public CreateSystemRuntimePolicyCommandValidator()
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

