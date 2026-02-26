using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Resources.Commands.UploadResource
{
    public record UploadResourceCommand() : IRequest<Result<UploadResourceRespone>>
    {
        public string FileName { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Description { get; init; }
        public Stream? FilePath { get; init; }
        public Guid SubjectId { get; init; }
    }
}
