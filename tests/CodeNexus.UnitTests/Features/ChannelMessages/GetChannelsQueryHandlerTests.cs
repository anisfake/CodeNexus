using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.ChannelMessages.Queries.GetChannels;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.ChannelMessages;

public class GetChannelsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetChannelsQueryHandler _handler;

    public GetChannelsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetChannelsQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_ValidSubjectAccess_ReturnsAllEnumChannels()
    {
        var userId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();

        var subjects = new List<Subject>
        {
            new()
            {
                SubjectId = subjectId,
                CreatedByUserId = userId,
                Name = "Programming",
                Category = SubjectCategory.ProgrammingLanguage,
                CreatedByUser = new User { UserId = userId, Username = "mentor" }
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Subjects).Returns(subjects.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetChannelsQuery(subjectId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Count.Should().Be(Enum.GetValues<SubjectCategory>().Length);
        result.Value.Select(x => x.Category).Should().BeEquivalentTo(Enum.GetValues<SubjectCategory>());
    }

    [Fact]
    public async Task Handle_SubjectNotFound_ReturnsFailure()
    {
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(NewId.NextGuid());
        _mockContext.Setup(x => x.Subjects).Returns(new List<Subject>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetChannelsQuery(NewId.NextGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("SUBJECT_NOT_FOUND");
    }
}
