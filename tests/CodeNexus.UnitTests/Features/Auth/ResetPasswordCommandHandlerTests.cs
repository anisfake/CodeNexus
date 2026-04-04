using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Auth.Commands.ResetPassword;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class ResetPasswordCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IOTPService> _otpServiceMock;
    private readonly ResetPasswordCommandHandler _handler;

    public ResetPasswordCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _tokenServiceMock = new Mock<ITokenService>();
        _tokenServiceMock.Setup(x => x.HashRefreshToken(It.IsAny<string>())).Returns((string s) => s);
        _otpServiceMock = new Mock<IOTPService>();
        _handler = new ResetPasswordCommandHandler(_contextMock.Object, _tokenServiceMock.Object, _otpServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenTokenInvalid_ReturnsFailure()
    {
        // Arrange
        var command = new ResetPasswordCommand("invalidToken", "NewPassword123");
        _tokenServiceMock.Setup(x => x.ValidateResetPasswordToken("invalidToken")).Returns((string?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_TOKEN");
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsFailure()
    {
        // Arrange
        var command = new ResetPasswordCommand("validToken", "NewPassword123");
        _tokenServiceMock.Setup(x => x.ValidateResetPasswordToken("validToken")).Returns("notfound@test.com");
        SetupUsersDbSet(new List<User>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenValidRequest_UpdatesPasswordAndReturnsSuccess()
    {
        // Arrange
        var command = new ResetPasswordCommand("validToken", "NewPassword123");
        var user = new User { Email = "test@test.com", Username = "testuser", PasswordHash = "oldHash" };
        _tokenServiceMock.Setup(x => x.ValidateResetPasswordToken("validToken")).Returns("test@test.com");
        _otpServiceMock.Setup(x => x.HashPassword("NewPassword123")).Returns("newHashedPassword");
        SetupUsersDbSet(new List<User> { user });
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        user.PasswordHash.Should().Be("newHashedPassword");
        _otpServiceMock.Verify(x => x.HashPassword("NewPassword123"), Times.Once);
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
