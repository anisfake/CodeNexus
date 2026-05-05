using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Users.Commands.CreateMentorAccount;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;

namespace CodeNexus.UnitTests.Features.Users;

public class CreateMentorAccountCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IPasswordService> _mockPasswordService;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<IAchievementService> _mockAchievementService;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<Microsoft.Extensions.Logging.ILogger<CreateMentorAccountCommandHandler>> _mockLogger;
    private readonly CreateMentorAccountCommandHandler _handler;

    public CreateMentorAccountCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockPasswordService = new Mock<IPasswordService>();
        _mockEmailService = new Mock<IEmailService>();
        _mockAchievementService = new Mock<IAchievementService>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<CreateMentorAccountCommandHandler>>();

        _handler = new CreateMentorAccountCommandHandler(
            _mockContext.Object,
            _mockPasswordService.Object,
            _mockEmailService.Object,
            _mockAchievementService.Object,
            _mockCurrentUserService.Object,
            _mockLogger.Object);
    }

    [Theory]
    [InlineData("Mentor")]
    [InlineData("Student")]
    public async Task Handle_WithValidRequest_ShouldCreateUserAndSendEmail(string role)
    {
        var users = new List<User>();
        var roles = new List<Role> { new() { RoleId = Guid.NewGuid(), RoleName = role } };
        var profiles = new List<UserProfile>();

        SetupUsersDbSet(users);
        SetupRolesDbSet(roles);
        SetupProfilesDbSet(profiles);

        _mockPasswordService.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("hashed");
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateMentorAccountCommand(
            "user@test.com",
            "user001",
            "First",
            "Last",
            "Some bio",
            "0123456789",
            "HCM",
            new DateTime(1995, 1, 1),
            role,
            true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Email.Should().Be("user@test.com");
        result.Value.Username.Should().Be("user001");
        result.Value.Role.Should().Be(role);
        result.Value.SetupEmailSent.Should().BeTrue();
        result.Value.TemporaryPassword.Should().NotBeNullOrWhiteSpace();

        users.Should().ContainSingle(u => u.Email == "user@test.com" && u.Username == "user001");
        profiles.Should().ContainSingle(p => p.Bio == "Some bio");

        _mockEmailService.Verify(x => x.SendNotificationEmailAsync(
                "user@test.com",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockAchievementService.Verify(x => x.InitializeUserAchievementsAsync(It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailExists_ShouldReturnFailure()
    {
        var users = new List<User>
        {
            new() { UserId = Guid.NewGuid(), Email = "user@test.com", Username = "user-old" }
        };
        SetupUsersDbSet(users);
        SetupRolesDbSet(new List<Role> { new() { RoleId = Guid.NewGuid(), RoleName = "Mentor" } });
        SetupProfilesDbSet(new List<UserProfile>());

        var command = BuildValidCommand("user@test.com", "user-new", "Mentor");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EMAIL_EXISTS");
    }

    [Fact]
    public async Task Handle_WhenUsernameExists_ShouldReturnFailure()
    {
        var users = new List<User>
        {
            new() { UserId = Guid.NewGuid(), Email = "other@test.com", Username = "takenuser" }
        };
        SetupUsersDbSet(users);
        SetupRolesDbSet(new List<Role> { new() { RoleId = Guid.NewGuid(), RoleName = "Student" } });
        SetupProfilesDbSet(new List<UserProfile>());

        var command = BuildValidCommand("new@test.com", "takenuser", "Student");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("USERNAME_EXISTS");
    }

    [Theory]
    [InlineData("Mentor")]
    [InlineData("Student")]
    public async Task Handle_WhenRoleMissing_ShouldReturnFailure(string role)
    {
        SetupUsersDbSet(new List<User>());
        SetupRolesDbSet(new List<Role>());
        SetupProfilesDbSet(new List<UserProfile>());

        var command = BuildValidCommand("user@test.com", "user001", role);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ROLE_NOT_FOUND");
    }

    [Theory]
    [InlineData("Mentor")]
    [InlineData("Student")]
    public async Task Handle_WhenUsernameNotProvided_ShouldAutoGenerateUniqueUsername(string role)
    {
        var users = new List<User>
        {
            new() { UserId = Guid.NewGuid(), Email = "old@test.com", Username = "newuser" }
        };
        var roles = new List<Role> { new() { RoleId = Guid.NewGuid(), RoleName = role } };
        var profiles = new List<UserProfile>();

        SetupUsersDbSet(users);
        SetupRolesDbSet(roles);
        SetupProfilesDbSet(profiles);

        _mockPasswordService.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("hashed");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateMentorAccountCommand(
            "newuser@company.com",
            null,
            "First",
            "Last",
            null,
            null,
            null,
            null,
            role,
            false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Username.Should().Be("newuser1");
        result.Value.SetupEmailSent.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenEmailSendingFails_ShouldStillCreateUser()
    {
        var users = new List<User>();
        var roles = new List<Role> { new() { RoleId = Guid.NewGuid(), RoleName = "Mentor" } };
        var profiles = new List<UserProfile>();

        SetupUsersDbSet(users);
        SetupRolesDbSet(roles);
        SetupProfilesDbSet(profiles);

        _mockPasswordService.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("hashed");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _mockEmailService.Setup(x => x.SendNotificationEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var command = BuildValidCommand("user@test.com", "user001", "Mentor");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.SetupEmailSent.Should().BeFalse();
        result.Value.SetupEmailError.Should().NotBeNullOrWhiteSpace();
    }

    private static CreateMentorAccountCommand BuildValidCommand(string email, string username, string role) =>
        new(email, username, "First", "Last", null, null, null, null, role, true);

    private void SetupUsersDbSet(List<User> users)
    {
        var usersDbSet = users.BuildMockDbSet();
        usersDbSet.Setup(x => x.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => users.Add(u))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<User>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<User>)null!));
        _mockContext.Setup(x => x.Users).Returns(usersDbSet.Object);
    }

    private void SetupRolesDbSet(List<Role> roles)
    {
        _mockContext.Setup(x => x.Roles).Returns(roles.BuildMockDbSet().Object);
    }

    private void SetupProfilesDbSet(List<UserProfile> profiles)
    {
        var profilesDbSet = profiles.BuildMockDbSet();
        profilesDbSet.Setup(x => x.AddAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
            .Callback<UserProfile, CancellationToken>((p, _) => profiles.Add(p))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<UserProfile>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<UserProfile>)null!));
        _mockContext.Setup(x => x.UserProfiles).Returns(profilesDbSet.Object);
    }
}
