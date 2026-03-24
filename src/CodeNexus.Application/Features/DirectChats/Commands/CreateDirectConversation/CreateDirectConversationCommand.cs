using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.DirectChats.Commands.CreateDirectConversation;

public record CreateDirectConversationCommand(Guid ParticipantId) : IRequest<Result<DirectConversationDto>>;
