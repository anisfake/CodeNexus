using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.DirectChats.Queries.GetUnreadCount;

public record GetUnreadCountQuery : IRequest<Result<DirectUnreadCountDto>>;
