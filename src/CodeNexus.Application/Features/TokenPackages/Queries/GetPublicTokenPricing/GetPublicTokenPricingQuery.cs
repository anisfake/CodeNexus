using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TokenPackages.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TokenPackages.Queries.GetPublicTokenPricing;

public record GetPublicTokenPricingQuery : IRequest<Result<PublicTokenPricingDto>>;

