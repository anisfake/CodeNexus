using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Subjects.Commands.DeleteSubject;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.UnitTests.Features.Subjects;

public class DeleteSubjectCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ISubjectCacheService> _mockSubjectCacheService;
    private readonly DeleteSubjectCommandHandler _handler;

    public DeleteSubjectCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockSubjectCacheService = new Mock<ISubjectCacheService>();
        _mockSubjectCacheService
            .Setup(x => x.InvalidateSubjectsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new DeleteSubjectCommandHandler(_mockContext.Object, _mockCurrentUserService.Object, _mockSubjectCacheService.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldSoftDeleteSubjectSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var command = new DeleteSubjectCommand(subjectId);

        var subject = new Subject
        {
            SubjectId = subjectId,
            Name = "Mathematics",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupSubjectsDbSet(new List<Subject> { subject });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("deleted successfully", result.Value);
        Assert.True(subject.IsDeleted);
        Assert.NotNull(subject.DeletedAt);
    }

    [Fact]
    public async Task Handle_WithNonExistentSubject_ShouldReturnNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var command = new DeleteSubjectCommand(subjectId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
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
        var command = new DeleteSubjectCommand(subjectId);

        var subject = new Subject
        {
            SubjectId = subjectId,
            Name = "Physics",
            CreatedByUserId = otherUserId,
            CreatedAt = DateTime.UtcNow
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupSubjectsDbSet(new List<Subject> { subject });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
        Assert.Contains("only delete subjects you created", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenSaveChangesFails_ShouldReturnFailure()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var command = new DeleteSubjectCommand(subjectId);

        var subject = new Subject
        {
            SubjectId = subjectId,
            Name = "Chemistry",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupSubjectsDbSet(new List<Subject> { subject });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("DELETE_SUBJECT_FAILED", result.ErrorCode);
        Assert.Contains("Database error", result.ErrorMessage);
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
