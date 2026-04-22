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

    [Fact]
    public async Task Handle_WithValidRequest_ShouldCreateMentorAndSendEmail()
    {
        var users = new List<User>();
        var roles = new List<Role> { new() { RoleId = Guid.NewGuid(), RoleName = "Mentor" } };
        var profiles = new List<UserProfile>();

        SetupUsersDbSet(users);
        SetupRolesDbSet(roles);
        SetupProfilesDbSet(profiles);

        _mockPasswordService.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("hashed");
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateMentorAccountCommand(
            "mentor@test.com",
            "mentor001",
            "Mentor",
            "User",
            "Backend mentor",
            "0123456789",
            "HCM",
            new DateTime(1995, 1, 1),
            true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Email.Should().Be("mentor@test.com");
        result.Value.Username.Should().Be("mentor001");
        result.Value.SetupEmailSent.Should().BeTrue();
        result.Value.TemporaryPassword.Should().NotBeNullOrWhiteSpace();

        users.Should().ContainSingle(u => u.Email == "mentor@test.com" && u.Username == "mentor001");
        profiles.Should().ContainSingle(p => p.Bio == "Backend mentor");

        _mockEmailService.Verify(x => x.SendNotificationEmailAsync(
                "mentor@test.com",
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
            new() { UserId = Guid.NewGuid(), Email = "mentor@test.com", Username = "mentor-old" }
        };
        SetupUsersDbSet(users);
        SetupRolesDbSet(new List<Role> { new() { RoleId = Guid.NewGuid(), RoleName = "Mentor" } });
        SetupProfilesDbSet(new List<UserProfile>());

        var command = new CreateMentorAccountCommand(
            "mentor@test.com",
            "mentor-new",
            "Mentor",
            "User",
            null,
            null,
            null,
            null,
            true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EMAIL_EXISTS");
    }

    [Fact]
    public async Task Handle_WhenRoleMissing_ShouldReturnFailure()
    {
        SetupUsersDbSet(new List<User>());
        SetupRolesDbSet(new List<Role>());
        SetupProfilesDbSet(new List<UserProfile>());

        var command = new CreateMentorAccountCommand(
            "mentor@test.com",
            "mentor001",
            "Mentor",
            "User",
            null,
            null,
            null,
            null,
            true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ROLE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenUsernameNotProvided_ShouldAutoGenerateUniqueUsername()
    {
        var users = new List<User>
        {
            new() { UserId = Guid.NewGuid(), Email = "old@test.com", Username = "mentor" }
        };
        var roles = new List<Role> { new() { RoleId = Guid.NewGuid(), RoleName = "Mentor" } };
        var profiles = new List<UserProfile>();

        SetupUsersDbSet(users);
        SetupRolesDbSet(roles);
        SetupProfilesDbSet(profiles);

        _mockPasswordService.Setup(x => x.HashPassword(It.IsAny<string>())).Returns("hashed");
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new CreateMentorAccountCommand(
            "mentor@company.com",
            null,
            "Mentor",
            "New",
            null,
            null,
            null,
            null,
            false);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Username.Should().Be("mentor1");
        result.Value.SetupEmailSent.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenEmailSendingFails_ShouldStillCreateMentor()
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
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP down"));

        var command = new CreateMentorAccountCommand(
            "mentor@test.com",
            "mentor001",
            "Mentor",
            "User",
            null,
            null,
            null,
            null,
            true);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.SetupEmailSent.Should().BeFalse();
        result.Value.SetupEmailError.Should().NotBeNullOrWhiteSpace();
    }

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
