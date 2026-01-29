using CodeNexus.Application.Common.Constants;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Auth.Commands.ForgotPassword;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class ForgotPasswordCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IOTPService> _otpServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly ForgotPasswordCommanHandler _handler;

    public ForgotPasswordCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _otpServiceMock = new Mock<IOTPService>();
        _emailServiceMock = new Mock<IEmailService>();
        _handler = new ForgotPasswordCommanHandler(_contextMock.Object, _otpServiceMock.Object, _emailServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsSuccessToPreventEnumeration()
    {
        // Arrange - Security: should return success even if user not found
        var command = new ForgotPasswordCommand("notfound@test.com");
        SetupUsersDbSet(new List<User>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Returns success to prevent email enumeration
        result.IsSuccess.Should().BeTrue();
        _emailServiceMock.Verify(x => x.SendOtpEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserExists_CreatesOtpAndSendsEmail()
    {
        // Arrange
        var command = new ForgotPasswordCommand("test@test.com");
        var user = new User { Email = "test@test.com", Username = "testuser" };
        SetupUsersDbSet(new List<User> { user });
        SetupOtpDbSet(new List<OtpVerification>());

        _otpServiceMock.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns("123456");
        _otpServiceMock.Setup(x => x.HashOtp("123456")).Returns("hashedOtp");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _emailServiceMock.Verify(x => x.SendOtpEmailAsync("test@test.com", "123456", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenRateLimited_ReturnsFailure()
    {
        // Arrange
        var command = new ForgotPasswordCommand("test@test.com");
        var user = new User { Email = "test@test.com", Username = "testuser" };
        var now = DateTime.Now;
        var existingOtp = new OtpVerification
        {
            Email = "test@test.com",
            Purpose = OtpPurpose.ResetPassword,
            LastResendAt = now // Just now - should be rate limited
        };
        SetupUsersDbSet(new List<User> { user });
        SetupOtpDbSet(new List<OtpVerification> { existingOtp });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("OTP_RATE_LIMITED");
    }

    [Fact]
    public async Task Handle_WhenExistingOtpExpired_RemovesOldAndCreatesNew()
    {
        // Arrange
        var command = new ForgotPasswordCommand("test@test.com");
        var user = new User { Email = "test@test.com", Username = "testuser" };
        var existingOtp = new OtpVerification
        {
            Email = "test@test.com",
            Purpose = OtpPurpose.ResetPassword,
            LastResendAt = DateTime.Now.AddMinutes(-5) // More than 1 minute ago
        };
        SetupUsersDbSet(new List<User> { user });
        SetupOtpDbSet(new List<OtpVerification> { existingOtp });

        _otpServiceMock.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns("123456");
        _otpServiceMock.Setup(x => x.HashOtp("123456")).Returns("hashedOtp");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
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
        _contextMock.Setup(x => x.Users).Returns(dbSetMock.Object);
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
        dbSetMock.Setup(x => x.Add(It.IsAny<OtpVerification>()));
        _contextMock.Setup(x => x.OtpVerification).Returns(dbSetMock.Object);
    }
}
