using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Subjects.Queries.GetSubjects;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Subjects;

public class GetSubjectsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly GetSubjectsQueryHandler _handler;

    public GetSubjectsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();

        _handler = new GetSubjectsQueryHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_HasSubjects_ReturnsAllSortedByCreatedAtDescending()
    {
        // Arrange
        var query = new GetSubjectsQuery();

        var user1 = new User
        {
            UserId = NewId.NextGuid(),
            FirstName = "John",
            LastName = "Doe",
            Username = "johndoe",
            Email = "john@test.com",
            PasswordHash = "hash"
        };

        var user2 = new User
        {
            UserId = NewId.NextGuid(),
            FirstName = "Jane",
            LastName = "Smith",
            Username = "janesmith",
            Email = "jane@test.com",
            PasswordHash = "hash"
        };

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = NewId.NextGuid(),
                CreatedByUserId = user1.UserId,
                CreatedByUser = user1,
                Name = "Python",
                Description = "Learn Python",
                Color = "#3776AB",
                Icon = "python",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                SubjectId = NewId.NextGuid(),
                CreatedByUserId = user2.UserId,
                CreatedByUser = user2,
                Name = "Java",
                Description = "Learn Java",
                Color = "#F89820",
                Icon = "java",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockContext.Setup(x => x.Subjects).Returns(
            subjects.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value![0].Name.Should().Be("Java");
        result.Value[0].CreatedBy.Should().Be("Jane Smith");
        result.Value[1].Name.Should().Be("Python");
        result.Value[1].CreatedBy.Should().Be("John Doe");
    }

    [Fact]
    public async Task Handle_NoSubjects_ReturnsEmptyList()
    {
        // Arrange
        var query = new GetSubjectsQuery();

        _mockContext.Setup(x => x.Subjects).Returns(
            new List<Subject>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithDeletedSubjects_ExcludesDeletedSubjects()
    {
        // Arrange
        var query = new GetSubjectsQuery();

        var user = new User
        {
            UserId = NewId.NextGuid(),
            FirstName = "John",
            LastName = "Doe",
            Username = "johndoe",
            Email = "john@test.com",
            PasswordHash = "hash"
        };

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = NewId.NextGuid(),
                CreatedByUserId = user.UserId,
                CreatedByUser = user,
                Name = "Active Subject",
                Description = "This is active",
                Color = "#3776AB",
                Icon = "active",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                SubjectId = NewId.NextGuid(),
                CreatedByUserId = user.UserId,
                CreatedByUser = user,
                Name = "Deleted Subject",
                Description = "This is deleted",
                Color = "#F89820",
                Icon = "deleted",
                IsDeleted = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        _mockContext.Setup(x => x.Subjects).Returns(
            subjects.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Name.Should().Be("Active Subject");
    }

    [Fact]
    public async Task Handle_ReturnsSubjectsWithCorrectUserNames()
    {
        // Arrange
        var query = new GetSubjectsQuery();

        var user = new User
        {
            UserId = NewId.NextGuid(),
            FirstName = "Alice",
            LastName = "Johnson",
            Username = "alicejohnson",
            Email = "alice@test.com",
            PasswordHash = "hash"
        };

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = NewId.NextGuid(),
                CreatedByUserId = user.UserId,
                CreatedByUser = user,
                Name = "C#",
                Description = "Learn C#",
                Color = "#239120",
                Icon = "csharp",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            }
        };

        _mockContext.Setup(x => x.Subjects).Returns(
            subjects.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].CreatedBy.Should().Be("Alice Johnson");
        result.Value[0].CreatedByUserId.Should().Be(user.UserId);
    }

    [Fact]
    public async Task Handle_ReturnsSubjectsWithAllProperties()
    {
        // Arrange
        var query = new GetSubjectsQuery();

        var user = new User
        {
            UserId = NewId.NextGuid(),
            FirstName = "Bob",
            LastName = "Wilson",
            Username = "bobwilson",
            Email = "bob@test.com",
            PasswordHash = "hash"
        };

        var subjectId = NewId.NextGuid();
        var createdAt = DateTime.UtcNow;

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = subjectId,
                CreatedByUserId = user.UserId,
                CreatedByUser = user,
                Name = "JavaScript",
                Description = "Learn JavaScript programming",
                Color = "#F7DF1E",
                Icon = "javascript",
                IsDeleted = false,
                CreatedAt = createdAt
            }
        };

        _mockContext.Setup(x => x.Subjects).Returns(
            subjects.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        
        var subject = result.Value![0];
        subject.SubjectId.Should().Be(subjectId);
        subject.Name.Should().Be("JavaScript");
        subject.Description.Should().Be("Learn JavaScript programming");
        subject.Color.Should().Be("#F7DF1E");
        subject.Icon.Should().Be("javascript");
        subject.CreatedBy.Should().Be("Bob Wilson");
        subject.CreatedByUserId.Should().Be(user.UserId);
        subject.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public async Task Handle_MultipleSubjects_ReturnsSortedByCreatedAtDescending()
    {
        // Arrange
        var query = new GetSubjectsQuery();

        var user = new User
        {
            UserId = NewId.NextGuid(),
            FirstName = "Test",
            LastName = "User",
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash"
        };

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = NewId.NextGuid(),
                CreatedByUserId = user.UserId,
                CreatedByUser = user,
                Name = "Oldest",
                Description = "Created first",
                Color = "#000000",
                Icon = "old",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                SubjectId = NewId.NextGuid(),
                CreatedByUserId = user.UserId,
                CreatedByUser = user,
                Name = "Newest",
                Description = "Created last",
                Color = "#FFFFFF",
                Icon = "new",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow
            },
            new()
            {
                SubjectId = NewId.NextGuid(),
                CreatedByUserId = user.UserId,
                CreatedByUser = user,
                Name = "Middle",
                Description = "Created in between",
                Color = "#888888",
                Icon = "mid",
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            }
        };

        _mockContext.Setup(x => x.Subjects).Returns(
            subjects.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value![0].Name.Should().Be("Newest");
        result.Value[1].Name.Should().Be("Middle");
        result.Value[2].Name.Should().Be("Oldest");
    }
}
