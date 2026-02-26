using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Resources.Queries.GetMyResources
{
    public class GetMyResourcesQueryValidator : AbstractValidator<GetMyResourcesQuery>
    {
        public GetMyResourcesQueryValidator()
        {
            RuleFor(x => x.SortBy)
                .IsInEnum()
                .WithMessage("SortBy must be a valid enum value.");
            RuleFor(x => x.PageNumber)
                .GreaterThan(0)
                .WithMessage("PageNumber must be greater than 0.");
            RuleFor(x => x.PageSize)
                .GreaterThan(0)
                .WithMessage("PageSize must be greater than 0.");
        }
    }
}
