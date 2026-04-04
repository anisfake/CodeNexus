using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAccessTierToAIUsageLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AIProviderConfigs_UsageType_AccessTier_IsActive",
                table: "AIProviderConfigs");

            migrationBuilder.AddColumn<string>(
                name: "AccessTierUsed",
                table: "AIUsageLogs",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "Free");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "AIUsageLogs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIUsageLogs_AccessTierUsed_UsageType_CreatedAt",
                table: "AIUsageLogs",
                columns: new[] { "AccessTierUsed", "UsageType", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AIUsageLogs_AccessTierUsed_UsageType_CreatedAt",
                table: "AIUsageLogs");

            migrationBuilder.DropColumn(
                name: "AccessTierUsed",
                table: "AIUsageLogs");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "AIUsageLogs");

            migrationBuilder.CreateIndex(
                name: "IX_AIProviderConfigs_UsageType_AccessTier_IsActive",
                table: "AIProviderConfigs",
                columns: new[] { "UsageType", "AccessTier", "IsActive" });
        }
    }
}
