using CodeNexus.Infrastructure.Services;
using FluentAssertions;

namespace CodeNexus.UnitTests.Services;

public class OTPServiceTests
{
    private readonly OTPService _otpService;

    public OTPServiceTests()
    {
        _otpService = new OTPService();
    }

    [Fact]
    public void GenerateOtp_ShouldReturn6DigitString()
    {
        // Act
        var otp = _otpService.GenerateOtp();

        // Assert
        otp.Should().HaveLength(6);
        otp.Should().MatchRegex(@"^\d{6}$");
    }

    [Fact]
    public void GenerateOtp_ShouldGenerateDifferentValues()
    {
        // Act
        var otps = Enumerable.Range(0, 10).Select(_ => _otpService.GenerateOtp()).ToList();

        // Assert - at least some should be different (statistically very likely)
        otps.Distinct().Count().Should().BeGreaterThan(1);
    }

    [Fact]
    public void HashOtp_ShouldReturnNonEmptyHash()
    {
        // Arrange
        var otp = "123456";

        // Act
        var hash = _otpService.HashOtp(otp);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(otp);
    }

    [Fact]
    public void HashOtp_ShouldReturnDifferentHashesForSameInput()
    {
        // Arrange - BCrypt generates different hashes due to salt
        var otp = "123456";

        // Act
        var hash1 = _otpService.HashOtp(otp);
        var hash2 = _otpService.HashOtp(otp);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void VerifyOtp_WhenCorrectOtp_ShouldReturnTrue()
    {
        // Arrange
        var otp = "123456";
        var hash = _otpService.HashOtp(otp);

        // Act
        var result = _otpService.VerifyOtp(otp, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyOtp_WhenIncorrectOtp_ShouldReturnFalse()
    {
        // Arrange
        var otp = "123456";
        var hash = _otpService.HashOtp(otp);

        // Act
        var result = _otpService.VerifyOtp("654321", hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HashPassword_ShouldReturnNonEmptyHash()
    {
        // Arrange
        var password = "MyPassword123";

        // Act
        var hash = _otpService.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
    }

    [Fact]
    public void HashPassword_ShouldBeVerifiableWithBCrypt()
    {
        // Arrange
        var password = "MyPassword123";

        // Act
        var hash = _otpService.HashPassword(password);

        // Assert
        BCrypt.Net.BCrypt.Verify(password, hash).Should().BeTrue();
    }
}
