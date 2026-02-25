using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetAllLearningPaths
{
    public class GetAllLearningPathQueryValidator : AbstractValidator<GetAllLearningPathQuery>
    {
        public GetAllLearningPathQueryValidator()
        {
            RuleFor(x => x.PageNumber)
                .GreaterThan(0).WithMessage("Page number must be greater than 0.");
            RuleFor(x => x.PageSize)
                .GreaterThan(0).WithMessage("Page size must be greater than 0.");
        }
    }
}
