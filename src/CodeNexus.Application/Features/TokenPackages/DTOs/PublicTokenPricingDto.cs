namespace CodeNexus.Application.Features.TokenPackages.DTOs;

public record PublicTokenPricingDto(
    decimal VndPerToken,
    decimal TokensPer1000Vnd,
    decimal MinimumTopUpVnd,
    decimal MaximumTopUpVnd
);

