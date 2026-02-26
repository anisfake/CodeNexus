using FluentValidation;

namespace CodeNexus.Application.Features.Resources.Queries.GetResourcePages
{
    public class GetResourcePagesQueryValidator : AbstractValidator<GetResourcePagesQuery>
    {
        public GetResourcePagesQueryValidator()
        {
            RuleFor(x => x.ResourceId)
                .NotEmpty()
                .WithMessage("ResourceId is required.");
        }
    }
}
