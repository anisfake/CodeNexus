using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.Commands.Register;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly Mock<IOTPCacheService> _otpCacheServiceMock;
    private readonly Mock<IPasswordService> _passwordServiceMock;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _emailServiceMock = new Mock<IEmailService>();
        _otpCacheServiceMock = new Mock<IOTPCacheService>();
        _passwordServiceMock = new Mock<IPasswordService>();
        _handler = new RegisterCommandHandler(_contextMock.Object, _emailServiceMock.Object, _otpCacheServiceMock.Object, _passwordServiceMock.Object);
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
    public async Task Handle_WhenValidRequest_CreatesOtpAndSendsEmail()
    {
        // Arrange
        var command = new RegisterCommand("new@test.com", "newuser", "John", "Doe", "Password123");
        var users = new List<User>();
        SetupUsersDbSet(users);

        _passwordServiceMock.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("hashedPassword");
        _otpCacheServiceMock.Setup(x => x.GenerateAndStoreOtpAsync(It.IsAny<string>(), It.IsAny<OtpPurpose>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _otpCacheServiceMock.Setup(x => x.GetLastGeneratedOtp()).Returns("123456");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _emailServiceMock.Verify(x => x.SendOtpEmailAsync("new@test.com", "123456", It.IsAny<CancellationToken>()), Times.Once);
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
