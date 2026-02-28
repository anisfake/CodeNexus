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
    public async Task Handle_HasSubjects_ReturnsAll()
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
        result.Value[1].Name.Should().Be("Python");
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
}
