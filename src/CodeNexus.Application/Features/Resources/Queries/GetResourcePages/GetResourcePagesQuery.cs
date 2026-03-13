using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using MediatR;
using System;

namespace CodeNexus.Application.Features.Resources.Queries.GetResourcePages
{
    public record GetResourcePagesQuery : IRequest<Result<ResourcePagesResponse>>
    {
        public Guid ResourceId { get; init; }
    }
}
