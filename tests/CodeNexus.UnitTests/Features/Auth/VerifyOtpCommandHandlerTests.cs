using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.Commands.VerifyOtp;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CodeNexus.UnitTests.Features.Auth;

public class VerifyOtpCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IOTPCacheService> _otpCacheServiceMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IAchievementService> _achievementServiceMock;
    private readonly VerifyOtpCommandHandler _handler;

    public VerifyOtpCommandHandlerTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _otpCacheServiceMock = new Mock<IOTPCacheService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _achievementServiceMock = new Mock<IAchievementService>();
        _tokenServiceMock.Setup(x => x.HashRefreshToken(It.IsAny<string>())).Returns((string s) => s);
        _achievementServiceMock.Setup(x => x.InitializeUserAchievementsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);
        _handler = new VerifyOtpCommandHandler(
            _contextMock.Object,
            _otpCacheServiceMock.Object,
            _tokenServiceMock.Object,
            _achievementServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoOtpFound_ReturnsFailure()
    {
        // Arrange
        var command = new VerifyOtpCommand("notfound@test.com", "123456");
        _otpCacheServiceMock.Setup(x => x.VerifyOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<(string, OtpPurpose)>.Failure("INVALID_OTP", "No pending verification found for this email"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_WhenOtpInvalid_ReturnsFailure()
    {
        // Arrange
        var command = new VerifyOtpCommand("test@test.com", "123456");
        _otpCacheServiceMock.Setup(x => x.VerifyOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<(string, OtpPurpose)>.Failure("INVALID_OTP", "Invalid OTP code"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("INVALID_OTP");
    }

    [Fact]
    public async Task Handle_WhenValidRegisterOtp_CreatesUserAndReturnsSuccess()
    {
        // Arrange
        var command = new VerifyOtpCommand("test@test.com", "123456");
        var registrationData = "testuser|hashedPassword|John|Doe";
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        
        _otpCacheServiceMock.Setup(x => x.VerifyOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<(string, OtpPurpose)>.Success((registrationData, OtpPurpose.Register)));
        _otpCacheServiceMock.Setup(x => x.DeleteOtpDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        
        SetupUsersDbSet(new List<User>());
        SetupUserProfilesDbSet(new List<UserProfile>());
        SetupRolesDbSet(new List<Role> { studentRole });
        
        _contextMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Purpose.Should().Be(OtpPurpose.Register);
        result.Value.Message.Should().Be("Registration successful");
        _achievementServiceMock.Verify(x => x.InitializeUserAchievementsAsync(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyExists_ReturnsFailure()
    {
        // Arrange
        var command = new VerifyOtpCommand("test@test.com", "123456");
        var registrationData = "testuser|hashedPassword|John|Doe";
        var existingUser = new User { Email = "test@test.com", Username = "testuser" };
        
        _otpCacheServiceMock.Setup(x => x.VerifyOtpAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<(string, OtpPurpose)>.Success((registrationData, OtpPurpose.Register)));
        
        SetupUsersDbSet(new List<User> { existingUser });
        SetupUserProfilesDbSet(new List<UserProfile>());
        SetupRolesDbSet(new List<Role>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USER_EXISTS");
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

    private void SetupRolesDbSet(List<Role> roles)
    {
        var queryable = new TestAsyncEnumerable<Role>(roles);
        var dbSetMock = new Mock<DbSet<Role>>();
        dbSetMock.As<IQueryable<Role>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<Role>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<Role>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<Role>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<Role>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _contextMock.Setup(x => x.Roles).Returns(dbSetMock.Object);
    }

    private void SetupUserProfilesDbSet(List<UserProfile> profiles)
    {
        var queryable = new TestAsyncEnumerable<UserProfile>(profiles);
        var dbSetMock = new Mock<DbSet<UserProfile>>();
        dbSetMock.As<IQueryable<UserProfile>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<UserProfile>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<UserProfile>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<UserProfile>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<UserProfile>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        dbSetMock.Setup(x => x.Add(It.IsAny<UserProfile>()));
        _contextMock.Setup(x => x.UserProfiles).Returns(dbSetMock.Object);
    }
}
