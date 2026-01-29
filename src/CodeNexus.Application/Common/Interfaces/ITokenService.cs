using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Common.Interfaces
{
    public interface ITokenService
    {
        string GenerateResetPasswordToken(string email);
        string? ValidateResetPasswordToken(string token);
    }
}
