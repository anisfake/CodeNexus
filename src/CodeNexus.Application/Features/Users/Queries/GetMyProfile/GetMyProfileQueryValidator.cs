using CodeNexus.Application.Features.Users.Commands.ChangePassword;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Users.Queries.GetMyProfile
{
    public class GetMyProfileQueryValidator : AbstractValidator<GetMyProfileQuery>
    {
        public GetMyProfileQueryValidator()
        {
            // No specific validation rules needed for this query
        }
    }
}
