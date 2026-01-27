using CodeNexus.Application.Common.Utilities;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using System.Text.RegularExpressions;

namespace CodeNexus.UnitTests.Features.Auth;

public class OtpGeneratorPropertyTests
{
    private const string SixDigitPattern = @"^\d{6}$";

    [Property(MaxTest = 100)]
    public Property GeneratedOtp_ShouldBeExactlySixNumericDigits()
    {
        return Prop.ForAll(Arb.From<int>(), _ =>
        {
            var otp = OtpGenerator.Generate();

            var isExactlySixDigits = Regex.IsMatch(otp, SixDigitPattern);
            var hasCorrectLength = otp.Length == 6;
            var isAllNumeric = otp.All(char.IsDigit);

            return (isExactlySixDigits && hasCorrectLength && isAllNumeric)
                .Label($"OTP '{otp}' should be exactly 6 numeric digits");
        });
    }
}
