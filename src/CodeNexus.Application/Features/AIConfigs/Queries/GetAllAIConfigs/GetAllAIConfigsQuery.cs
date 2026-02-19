using MediatR;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Application.Common.Models;

namespace CodeNexus.Application.Features.AIConfigs.Queries.GetAllAIConfigs
{
    public record GetAllAIConfigsQuery() : IRequest<Result<List<GetAllAIConfigResponse>>>;
}
