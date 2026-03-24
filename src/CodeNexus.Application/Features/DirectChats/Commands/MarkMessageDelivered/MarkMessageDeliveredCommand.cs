using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.DirectChats.Commands.MarkMessageDelivered;

public record MarkMessageDeliveredCommand(Guid MessageId) : IRequest<Result>;
