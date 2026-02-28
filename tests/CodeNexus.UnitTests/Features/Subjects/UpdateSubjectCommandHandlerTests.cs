using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Subjects.Commands.UpdateSubject;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.Subjects;

public class UpdateSubjectCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly UpdateSubjectCommandHandler _handler;

    public UpdateSubjectCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new UpdateSubjectCommandHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldUpdateSubjectSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var command = new UpdateSubjectCommand(subjectId, "Updated Math", "New Description", "#AABBCC", "new-icon");

        var user = new User
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe",
            Username = "johndoe",
            Email = "john@test.com",
            PasswordHash = "hash"
        };

        var subject = new Subject
        {
            SubjectId = subjectId,
            Name = "Mathematics",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupUsersDbSet(new List<User> { user });
        SetupSubjectsDbSet(new List<Subject> { subject });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Updated Math", result.Value.Name);
        Assert.Equal("New Description", result.Value.Description);
    }

    [Fact]
    public async Task Handle_WithNonExistentSubject_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var command = new UpdateSubjectCommand(subjectId, "Physics", null, null, null);

        var user = new User
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe",
            Username = "johndoe",
            Email = "john@test.com",
            PasswordHash = "hash"
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupUsersDbSet(new List<User> { user });
        SetupSubjectsDbSet(new List<Subject>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SUBJECT_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WithDifferentCreator_ShouldReturnUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var command = new UpdateSubjectCommand(subjectId, "Chemistry", null, null, null);

        var user = new User
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe",
            Username = "johndoe",
            Email = "john@test.com",
            PasswordHash = "hash"
        };

        var subject = new Subject
        {
            SubjectId = subjectId,
            Name = "Chemistry",
            CreatedByUserId = otherUserId,
            CreatedAt = DateTime.UtcNow
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupUsersDbSet(new List<User> { user });
        SetupSubjectsDbSet(new List<Subject> { subject });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Contains("only update subjects you created", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithDuplicateName_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var command = new UpdateSubjectCommand(subjectId, "Biology", null, null, null);

        var user = new User
        {
            UserId = userId,
            FirstName = "John",
            LastName = "Doe",
            Username = "johndoe",
            Email = "john@test.com",
            PasswordHash = "hash"
        };

        var subject = new Subject
        {
            SubjectId = subjectId,
            Name = "Chemistry",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        var existingSubject = new Subject
        {
            SubjectId = Guid.NewGuid(),
            Name = "Biology",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupUsersDbSet(new List<User> { user });
        SetupSubjectsDbSet(new List<Subject> { subject, existingSubject });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SUBJECT_EXISTS", result.ErrorCode);
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
