using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.ChannelMessages.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.ChannelMessages.Queries.GetChannels;

public record GetChannelsQuery(Guid SubjectId) : IRequest<Result<List<ChannelDto>>>;
