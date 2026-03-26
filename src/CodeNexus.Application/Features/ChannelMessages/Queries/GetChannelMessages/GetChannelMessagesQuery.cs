using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.ChannelMessages.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.ChannelMessages.Queries.GetChannelMessages;

public record GetChannelMessagesQuery(
    Guid SubjectId,
    SubjectCategory Category,
    int PageNumber = 1,
    int PageSize = 30
) : IRequest<Result<PaginationDto<ChannelMessageDto>>>;
