using CodeNexus.API.Hubs;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.Commands.SendTutorMessage;
using CodeNexus.Application.Features.TutorChat.DTOs;
using CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationMessages;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Hubs;

public class TutorChatHubTests
{
    private readonly Mock<ISender> _mockSender;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<ISingleClientProxy> _mockClientProxy;
    private readonly TutorChatHub _hub;

    public TutorChatHubTests()
    {
        _mockSender = new Mock<ISender>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<ISingleClientProxy>();

        _mockClients.Setup(x => x.Caller).Returns(_mockClientProxy.Object);

        _hub = new TutorChatHub(_mockSender.Object);

        var clientsProperty = typeof(Hub).GetProperty("Clients");
        clientsProperty?.SetValue(_hub, _mockClients.Object);
    }

    [Fact]
    public async Task SendTutorMessage_WithSuccess_ShouldEmitReceived()
    {
        var response = new TutorChatResponseDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Tutor reply",
            DateTime.UtcNow);

        _mockSender.Setup(x => x.Send(It.IsAny<SendTutorMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<TutorChatResponseDto>.Success(response));

        await _hub.SendTutorMessage(null, null, null, null, "Hello");

        _mockClientProxy.Verify(x => x.SendCoreAsync(
            "TutorMessageReceived",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestTutorMessages_WithSuccess_ShouldEmitLoaded()
    {
        var pagination = new PaginationDto<TutorMessageDto>
        {
            Items = new List<TutorMessageDto>(),
            PageNumber = 1,
            PageSize = 30,
            TotalCount = 0
        };

        _mockSender.Setup(x => x.Send(It.IsAny<GetTutorConversationMessagesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PaginationDto<TutorMessageDto>>.Success(pagination));

        await _hub.RequestTutorMessages(Guid.NewGuid());

        _mockClientProxy.Verify(x => x.SendCoreAsync(
            "TutorMessagesLoaded",
            It.IsAny<object[]>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
