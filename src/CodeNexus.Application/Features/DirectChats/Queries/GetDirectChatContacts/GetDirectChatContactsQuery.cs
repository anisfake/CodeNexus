using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.DirectChats.Queries.GetDirectChatContacts;

public record GetDirectChatContactsQuery : IRequest<Result<List<DirectChatContactDto>>>;
