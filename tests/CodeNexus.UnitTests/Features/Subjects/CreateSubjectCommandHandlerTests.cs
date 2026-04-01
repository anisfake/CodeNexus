using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Subjects.Commands.CreateSubject;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.Subjects;

public class CreateSubjectCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly CreateSubjectCommandHandler _handler;

    public CreateSubjectCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new CreateSubjectCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WithMentorRole_ShouldCreateSubjectSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var command = new CreateSubjectCommand("Mathematics", "Advanced Math", "#FF5733", "math-icon", SubjectCategory.Other);

        var mentorRole = new Role { RoleId = roleId, RoleName = "Mentor" };
        var user = new User { UserId = userId, RoleId = roleId, Role = mentorRole };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupUsersDbSet(new List<User> { user });
        SetupSubjectsDbSet(new List<Subject>());
        _mockContext.Setup(x => x.Subjects.AddAsync(It.IsAny<Subject>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Subject>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Subject>)null!));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Mathematics", result.Value.Name);
        Assert.Equal("Advanced Math", result.Value.Description);
        Assert.Equal("#FF5733", result.Value.Color);
        Assert.Equal("math-icon", result.Value.Icon);
    }

    [Fact]
    public async Task Handle_WithStudentRole_ShouldReturnUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var command = new CreateSubjectCommand("Physics", "Basic Physics", null, null, SubjectCategory.Other);

        var studentRole = new Role { RoleId = roleId, RoleName = "Student" };
        var user = new User { UserId = userId, RoleId = roleId, Role = studentRole };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupUsersDbSet(new List<User> { user });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Contains("User not authenticated", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithDuplicateSubjectName_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var command = new CreateSubjectCommand("Chemistry", "Organic Chemistry", null, null, SubjectCategory.Other);

        var mentorRole = new Role { RoleId = roleId, RoleName = "Mentor" };
        var user = new User { UserId = userId, RoleId = roleId, Role = mentorRole };
        var existingSubject = new Subject { SubjectId = Guid.NewGuid(), Name = "Chemistry" };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupUsersDbSet(new List<User> { user });
        SetupSubjectsDbSet(new List<Subject> { existingSubject });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SUBJECT_EXISTS", result.ErrorCode);
        Assert.Contains("already exists", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var command = new CreateSubjectCommand("Biology", "Cell Biology", null, null, SubjectCategory.Other);

        var mentorRole = new Role { RoleId = roleId, RoleName = "Mentor" };
        var user = new User { UserId = userId, RoleId = roleId, Role = mentorRole };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupUsersDbSet(new List<User> { user });
        SetupSubjectsDbSet(new List<Subject>());
        _mockContext.Setup(x => x.Subjects.AddAsync(It.IsAny<Subject>(), It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Subject>>(
                (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Subject>)null!));
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("ADD_SUBJECT_FAILED", result.ErrorCode);
        Assert.Contains("Database error", result.ErrorMessage);
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
        _mockContext.Setup(x => x.Users).Returns(dbSetMock.Object);
    }

    private void SetupSubjectsDbSet(List<Subject> subjects)
    {
        var queryable = new TestAsyncEnumerable<Subject>(subjects);
        var dbSetMock = new Mock<DbSet<Subject>>();
        dbSetMock.As<IQueryable<Subject>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<Subject>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<Subject>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<Subject>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<Subject>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.Subjects).Returns(dbSetMock.Object);
    }
}
