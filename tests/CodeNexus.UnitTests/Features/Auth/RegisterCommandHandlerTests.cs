using CodeNexus.Application.Common.Constants;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Auth.Commands.Register;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IOTPService> _otpServiceMock;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _emailServiceMock = new Mock<IEmailService>();
        _otpServiceMock = new Mock<IOTPService>();
        _handler = new RegisterCommandHandler(_contextMock.Object, _emailServiceMock.Object, _otpServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var command = new RegisterCommand("existing@test.com", "newuser", "John", "Doe", "Password123");
        var users = new List<User> { new() { Email = "existing@test.com", Username = "existinguser" } };
        SetupUsersDbSet(users);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EMAIL_EXISTS");
    }

    [Fact]
    public async Task Handle_WhenUsernameAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var command = new RegisterCommand("new@test.com", "existinguser", "John", "Doe", "Password123");
        var users = new List<User> { new() { Email = "other@test.com", Username = "existinguser" } };
        SetupUsersDbSet(users);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USERNAME_EXISTS");
    }


    [Fact]
    public async Task Handle_WhenOtpRateLimited_ReturnsFailure()
    {
        // Arrange
        var command = new RegisterCommand("test@test.com", "newuser", "John", "Doe", "Password123");
        var users = new List<User>();
        SetupUsersDbSet(users);

        var existingOtp = new OtpVerification
        {
            Email = "test@test.com",
            LastResendAt = DateTime.Now // Just now - should be rate limited
        };
        var otpList = new List<OtpVerification> { existingOtp };
        SetupOtpDbSet(otpList);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("OTP_RATE_LIMITED");
    }

    [Fact]
    public async Task Handle_WhenValidRequest_CreatesOtpAndSendsEmail()
    {
        // Arrange
        var command = new RegisterCommand("new@test.com", "newuser", "John", "Doe", "Password123");
        var users = new List<User>();
        SetupUsersDbSet(users);

        var otpList = new List<OtpVerification>();
        SetupOtpDbSet(otpList);

        _otpServiceMock.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns("123456");
        _otpServiceMock.Setup(x => x.HashOtp(It.IsAny<string>())).Returns("hashedOtp");
        _otpServiceMock.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("hashedPassword");
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _emailServiceMock.Verify(x => x.SendOtpEmailAsync("new@test.com", "123456", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenExistingOtpExpired_RemovesOldAndCreatesNew()
    {
        // Arrange
        var command = new RegisterCommand("test@test.com", "newuser", "John", "Doe", "Password123");
        var users = new List<User>();
        SetupUsersDbSet(users);

        var existingOtp = new OtpVerification
        {
            Email = "test@test.com",
            LastResendAt = DateTime.Now.AddMinutes(-5) // More than 1 minute ago
        };
        var otpList = new List<OtpVerification> { existingOtp };
        SetupOtpDbSet(otpList);

        _otpServiceMock.Setup(x => x.GenerateOtp(It.IsAny<int>())).Returns("123456");
        _otpServiceMock.Setup(x => x.HashOtp(It.IsAny<string>())).Returns("hashedOtp");
        _otpServiceMock.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("hashedPassword");
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
