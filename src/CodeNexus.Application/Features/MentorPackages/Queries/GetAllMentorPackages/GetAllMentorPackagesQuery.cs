using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorPackages.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.MentorPackages.Queries.GetAllMentorPackages;

public record GetAllMentorPackagesQuery(bool ActiveOnly = false) : IRequest<Result<List<MentorPackageDto>>>;
