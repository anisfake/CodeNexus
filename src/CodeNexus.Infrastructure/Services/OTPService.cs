using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
namespace CodeNexus.Infrastructure.Services;

using BCrypt.Net;
using CodeNexus.Application.Common.Interfaces;
using static System.Net.WebRequestMethods;

public class OTPService : IOTPService
{
    private const int OtpLength = 6;
    public string GenerateOtp(int length = 6)
    {
        var randomNumber = RandomNumberGenerator.GetInt32(0, 1_000_000);

        return randomNumber.ToString().PadLeft(OtpLength, '0');
    }
    public string HashOtp(string otp)
    {
        return BCrypt.HashPassword(otp);
    }

    public string HashPassword(string password)
    {
        return BCrypt.HashPassword(password);
    }

    public bool VerifyOtp(string inputOtp, string hashedOtp)
    {
        return BCrypt.Verify(inputOtp, hashedOtp);
    }
}

