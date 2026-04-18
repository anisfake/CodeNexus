namespace CodeNexus.Application.Features.AIUsageLogs.DTOs;

public record AIProfitOverviewResponse(
    DateTime? FromDate,
    DateTime? ToDate,
    decimal SystemCostFreeUsd,
    decimal SystemCostPaidUsd,
    decimal SystemCostTotalUsd,
    decimal StudentUsageFreeUsd,
    decimal StudentUsagePaidUsd,
    decimal StudentUsageCostUsd,
    decimal StudentUsageRawUsd,
    decimal StudentBilledRevenueUsd,
    decimal TotalRevenueFreeUsd,
    decimal TotalRevenuePaidUsd,
    decimal TotalRevenueUsd,
    decimal TotalProfitUsd,
    decimal StudentRevenueRawUsd,
    decimal ProfitUsd
);
