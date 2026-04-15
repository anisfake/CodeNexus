using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TokenPackages.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TokenPackages.Queries.GetAllTokenPackages;

public record GetAllTokenPackagesQuery(bool ActiveOnly = false) : IRequest<Result<List<TokenPackageDto>>>;
