using CodeNexus.Application.Common.Constants;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Auth.Commands.VerifyOtp;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class VerifyOtpCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IOTPService> _otpServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly VerifyOtpCommandHandler _handler;

    public VerifyOtpCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _otpServiceMock = new Mock<IOTPService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _handler = new VerifyOtpCommandHandler(_contextMock.Object, _otpServiceMock.Object, _tokenServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoOtpFound_ReturnsFailure()
    {
        // Arrange
        var command = new VerifyOtpCommand("notfound@test.com", "123456");
        SetupOtpDbSet(new List<OtpVerification>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_WhenMaxAttemptsExceeded_RemovesOtpAndReturnsFailure()
    {
        // Arrange
        var command = new VerifyOtpCommand("test@test.com", "123456");
        var otp = new OtpVerification
        {
            Email = "test@test.com",
            AttemptCount = 5,
            ExpiresAt = DateTime.Now.AddMinutes(5)
        };
        SetupOtpDbSet(new List<OtpVerification> { otp });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MAX_ATTEMPTS_EXCEEDED");
    }

    [Fact]
    public async Task Handle_WhenOtpExpired_ReturnsFailure()
    {
        // Arrange
        var command = new VerifyOtpCommand("test@test.com", "123456");
        var otp = new OtpVerification
        {
            Email = "test@test.com",
            AttemptCount = 0,
            ExpiresAt = DateTime.Now.AddMinutes(-1) // Expired
        };
        SetupOtpDbSet(new List<OtpVerification> { otp });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("OTP_EXPIRED");
    }


    [Fact]
    public async Task Handle_WhenOtpInvalid_IncrementsAttemptCount()
    {
        // Arrange
        var command = new VerifyOtpCommand("test@test.com", "123456");
        var otp = new OtpVerification
        {
            Email = "test@test.com",
            AttemptCount = 0,
            ExpiresAt = DateTime.Now.AddMinutes(5),
            OtpHash = "hashedOtp"
        };
        SetupOtpDbSet(new List<OtpVerification> { otp });
        _otpServiceMock.Setup(x => x.VerifyOtp("123456", "hashedOtp")).Returns(false);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_OTP");
        otp.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenValidRegisterOtp_CreatesUserAndReturnsSuccess()
    {
        // Arrange
        var command = new VerifyOtpCommand("test@test.com", "123456");
        var otp = new OtpVerification
        {
            Email = "test@test.com",
            Username = "testuser",
            FirstName = "John",
            LastName = "Doe",
            PasswordHash = "hashedPassword",
            AttemptCount = 0,
            ExpiresAt = DateTime.Now.AddMinutes(5),
            OtpHash = "hashedOtp",
            Purpose = OtpPurpose.Register
        };
        SetupOtpDbSet(new List<OtpVerification> { otp });
        SetupUsersDbSet(new List<User>());
        _otpServiceMock.Setup(x => x.VerifyOtp("123456", "hashedOtp")).Returns(true);
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Purpose.Should().Be(OtpPurpose.Register);
        result.Value.Message.Should().Be("Registration successful");
    }

    [Fact]
    public async Task Handle_WhenValidResetPasswordOtp_ReturnsResetToken()
    {
        // Arrange
        var command = new VerifyOtpCommand("test@test.com", "123456");
        var otp = new OtpVerification
        {
            Email = "test@test.com",
            AttemptCount = 0,
            ExpiresAt = DateTime.Now.AddMinutes(5),
            OtpHash = "hashedOtp",
            Purpose = OtpPurpose.ResetPassword
        };
        SetupOtpDbSet(new List<OtpVerification> { otp });
        _otpServiceMock.Setup(x => x.VerifyOtp("123456", "hashedOtp")).Returns(true);
        _tokenServiceMock.Setup(x => x.GenerateResetPasswordToken("test@test.com")).Returns("resetToken123");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Purpose.Should().Be(OtpPurpose.ResetPassword);
        result.Value.ResetToken.Should().Be("resetToken123");
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var command = new VerifyOtpCommand("test@test.com", "123456");
        var otp = new OtpVerification
        {
            Email = "test@test.com",
            Username = "testuser",
            AttemptCount = 0,
            ExpiresAt = DateTime.Now.AddMinutes(5),
            OtpHash = "hashedOtp",
            Purpose = OtpPurpose.Register
        };
        var existingUser = new User { Email = "test@test.com", Username = "testuser" };
        SetupOtpDbSet(new List<OtpVerification> { otp });
        SetupUsersDbSet(new List<User> { existingUser });
        _otpServiceMock.Setup(x => x.VerifyOtp("123456", "hashedOtp")).Returns(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_EXISTS");
    }

    private void SetupOtpDbSet(List<OtpVerification> otpList)
    {
        var queryable = new TestAsyncEnumerable<OtpVerification>(otpList);
        var dbSetMock = new Mock<DbSet<OtpVerification>>();
        dbSetMock.As<IQueryable<OtpVerification>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<OtpVerification>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<OtpVerification>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<OtpVerification>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<OtpVerification>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        dbSetMock.Setup(x => x.Remove(It.IsAny<OtpVerification>()));
        _contextMock.Setup(x => x.OtpVerification).Returns(dbSetMock.Object);
    }

    private void SetupUsersDbSet(List<User> users)
    {
        var queryable = new TestAsyncEnumerable<User>(users);
        var dbSetMock = new Mock<DbSet<User>>();
        dbSetMock.As<IQueryable<User>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<User>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<User>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<User>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<User>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        dbSetMock.Setup(x => x.Add(It.IsAny<User>()));
        _contextMock.Setup(x => x.Users).Returns(dbSetMock.Object);
    }
}
