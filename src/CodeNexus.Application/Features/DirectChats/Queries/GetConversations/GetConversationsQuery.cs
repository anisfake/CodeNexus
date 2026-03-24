using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.DirectChats.Queries.GetConversations;

public record GetConversationsQuery : IRequest<Result<List<DirectConversationDto>>>;
