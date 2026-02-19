using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.Commands.ForgotPassword;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class ForgotPasswordCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IOTPCacheService> _otpCacheServiceMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly ForgotPasswordCommanHandler _handler;

    public ForgotPasswordCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _otpCacheServiceMock = new Mock<IOTPCacheService>();
        _emailServiceMock = new Mock<IEmailService>();
        _handler = new ForgotPasswordCommanHandler(_contextMock.Object, _otpCacheServiceMock.Object, _emailServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenRateLimited_ReturnsFailure()
    {
        // Arrange
        var command = new ForgotPasswordCommand("test@test.com");
        var users = new List<User> { new() { Email = "test@test.com", Username = "testuser" } };
        SetupUsersDbSet(users);
        
        _otpCacheServiceMock.Setup(x => x.GenerateAndStoreOtpAsync(
            It.IsAny<string>(), 
            It.IsAny<OtpPurpose>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("OTP_RATE_LIMITED", "Please wait 1 minute before requesting a new OTP"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("OTP_RATE_LIMITED");
    }

    [Fact]
    public async Task Handle_WhenValidRequest_GeneratesOtpAndSendsEmail()
    {
        // Arrange
        var command = new ForgotPasswordCommand("test@test.com");
        var users = new List<User> { new() { Email = "test@test.com", Username = "testuser" } };
        SetupUsersDbSet(users);
        
        _otpCacheServiceMock.Setup(x => x.GenerateAndStoreOtpAsync(
            It.IsAny<string>(), 
            It.IsAny<OtpPurpose>(), 
            It.IsAny<string>(), 
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        
        _otpCacheServiceMock.Setup(x => x.GetLastGeneratedOtp()).Returns("123456");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _emailServiceMock.Verify(x => x.SendOtpEmailAsync("test@test.com", "123456", It.IsAny<CancellationToken>()), Times.Once);
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
}


