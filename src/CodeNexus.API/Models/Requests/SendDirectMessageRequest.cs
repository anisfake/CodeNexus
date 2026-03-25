using CodeNexus.Domain.Enums;

namespace CodeNexus.API.Models.Requests;

public record SendDirectMessageRequest(
    string Content,
    DirectMessageType MessageType = DirectMessageType.Text,
    Guid? ReplyToMessageId = null
);
