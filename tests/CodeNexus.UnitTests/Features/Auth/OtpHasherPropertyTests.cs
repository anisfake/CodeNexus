using CodeNexus.Application.Common.Utilities;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;

namespace CodeNexus.UnitTests.Features.Auth;

public class OtpHasherPropertyTests
{
    [Property(MaxTest = 100)]
    public Property HashedOtp_ShouldNotEqualPlaintextOtp()
    {
        return Prop.ForAll(SixDigitOtpGenerator(), otp =>
        {
            // Act
            var hash = OtpHasher.Hash(otp);

            // Assert - The hash should NOT equal the plaintext OTP
            var hashNotEqualToPlaintext = hash != otp;
            var hashIsNotEmpty = !string.IsNullOrEmpty(hash);
            var hashIsDifferentLength = hash.Length != otp.Length;

            return (hashNotEqualToPlaintext && hashIsNotEmpty && hashIsDifferentLength)
                .Label($"Hash of OTP '{otp}' should not equal plaintext. Hash: '{hash}'");
        });
    }

    [Property(MaxTest = 100)]
    public Property HashedOtp_ShouldVerifyCorrectly()
    {
        return Prop.ForAll(SixDigitOtpGenerator(), otp =>
        {
            // Act
            var hash = OtpHasher.Hash(otp);
            var verifyResult = OtpHasher.Verify(otp, hash);

            // Assert
            return verifyResult.Label($"OTP '{otp}' should verify against its own hash");
        });
    }
    private static Arbitrary<string> SixDigitOtpGenerator()
    {
        var otpGen = Gen.Choose(0, 999999)
            .Select(n => n.ToString().PadLeft(6, '0'));

        return Arb.From(otpGen);
    }
}
