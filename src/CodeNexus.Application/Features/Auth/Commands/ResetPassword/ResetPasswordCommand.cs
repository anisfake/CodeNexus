using CodeNexus.Application.Common.Models;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Auth.Commands.ResetPassword
{
    public record ResetPasswordCommand(string ResetToken, string NewPassword) : IRequest<Result>;
}
