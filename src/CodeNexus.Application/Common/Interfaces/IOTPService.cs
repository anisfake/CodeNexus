using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Common.Interfaces
{
    public interface IOTPService
    {
        string GenerateOtp(int length = 6);
        string HashOtp(string otp);
        bool VerifyOtp(string inputOtp, string hashedOtp);
        string HashPassword(string password);
    }
}
