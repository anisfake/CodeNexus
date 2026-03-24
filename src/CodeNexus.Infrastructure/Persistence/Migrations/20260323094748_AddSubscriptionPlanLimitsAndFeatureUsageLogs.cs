using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionPlanLimitsAndFeatureUsageLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeatureUsageLogs",
                columns: table => new
                {
                    FeatureUsageLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureUsageLogs", x => x.FeatureUsageLogId);
                    table.ForeignKey(
                        name: "FK_FeatureUsageLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanLimits",
                columns: table => new
                {
                    SubscriptionPlanLimitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LimitCount = table.Column<int>(type: "int", nullable: true),
                    WindowType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlanLimits", x => x.SubscriptionPlanLimitId);
                    table.ForeignKey(
                        name: "FK_SubscriptionPlanLimits_SubscriptionPlans_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "SubscriptionPlanId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureUsageLogs_UserId_FeatureKey_CreatedAt",
                table: "FeatureUsageLogs",
                columns: new[] { "UserId", "FeatureKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanLimits_SubscriptionPlanId_FeatureKey",
                table: "SubscriptionPlanLimits",
                columns: new[] { "SubscriptionPlanId", "FeatureKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeatureUsageLogs");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanLimits");
        }
    }
}
