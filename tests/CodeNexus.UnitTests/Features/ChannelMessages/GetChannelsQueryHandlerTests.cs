using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.ChannelMessages.Queries.GetChannels;
using CodeNexus.Domain.Enums;
using FluentAssertions;
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
    public async Task Handle_AuthorizedUser_ReturnsAllCategories()
    {
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());

        var result = await _handler.Handle(new GetChannelsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Count.Should().Be(Enum.GetValues<SubjectCategory>().Length);
    }

    [Fact]
    public async Task Handle_Unauthorized_ReturnsFailure()
    {
        _mockCurrentUserService.Setup(x => x.GetUserId()).Throws(new Exception("no user"));

        var result = await _handler.Handle(new GetChannelsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }
}
