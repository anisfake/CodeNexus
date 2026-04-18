namespace CodeNexus.Application.Features.AIUsageLogs.DTOs;

public record AIProfitOverviewResponse(
    DateTime? FromDate,
    DateTime? ToDate,
    decimal SystemCostFreeUsd,
    decimal SystemCostPaidUsd,
    decimal SystemCostTotalUsd,
    decimal StudentUsageCostUsd,
    decimal StudentRevenueRawUsd,
    decimal ProfitUsd
);
