using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Resources.DTOs
{
    public record ResourceResponse(
        string Title,
        string Type,
        string? Url,
        string? Description,
        string? FilePath,
        string SubjectName,
        Guid SubjectId
    );
}
