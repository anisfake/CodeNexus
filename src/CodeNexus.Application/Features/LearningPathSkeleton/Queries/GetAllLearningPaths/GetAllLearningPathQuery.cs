using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetAllLearningPaths
{
    public record GetAllLearningPathQuery() : IRequest<Result<List<LearningPathResponse>>>;
}
