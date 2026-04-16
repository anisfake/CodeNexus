using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeprecatedSubscriptionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_SubscriptionPlans_SubscriptionPlanId",
                table: "PaymentTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_SubscriptionPlans_SubscriptionPlanId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "SubscriptionPlanLimits");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropIndex(
                name: "IX_Users_SubscriptionPlanId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_SubscriptionPlanId",
                table: "PaymentTransactions");

            migrationBuilder.AddColumn<decimal>(
                name: "BalanceVnd",
                table: "Users",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CreditedAmountVnd",
                table: "PaymentTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TokenPackageId",
                table: "PaymentTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TokenPackages",
                columns: table => new
                {
                    TokenPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PriceVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditedBalanceVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenPackages", x => x.TokenPackageId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TokenPackageId",
                table: "PaymentTransactions",
                column: "TokenPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_TokenPackages_DisplayOrder",
                table: "TokenPackages",
                column: "DisplayOrder");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_TokenPackages_TokenPackageId",
                table: "PaymentTransactions",
                column: "TokenPackageId",
                principalTable: "TokenPackages",
                principalColumn: "TokenPackageId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_TokenPackages_TokenPackageId",
                table: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "TokenPackages");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_TokenPackageId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "BalanceVnd",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreditedAmountVnd",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "TokenPackageId",
                table: "PaymentTransactions");

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PlanType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PriceVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.SubscriptionPlanId);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlanLimits",
                columns: table => new
                {
                    SubscriptionPlanLimitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LimitCount = table.Column<int>(type: "int", nullable: true),
                    WindowType = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
                name: "IX_Users_SubscriptionPlanId",
                table: "Users",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_SubscriptionPlanId",
                table: "PaymentTransactions",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlanLimits_SubscriptionPlanId_FeatureKey",
                table: "SubscriptionPlanLimits",
                columns: new[] { "SubscriptionPlanId", "FeatureKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_DisplayOrder",
                table: "SubscriptionPlans",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_PlanType",
                table: "SubscriptionPlans",
                column: "PlanType",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_SubscriptionPlans_SubscriptionPlanId",
                table: "PaymentTransactions",
                column: "SubscriptionPlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "SubscriptionPlanId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_SubscriptionPlans_SubscriptionPlanId",
                table: "Users",
                column: "SubscriptionPlanId",
                principalTable: "SubscriptionPlans",
                principalColumn: "SubscriptionPlanId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
