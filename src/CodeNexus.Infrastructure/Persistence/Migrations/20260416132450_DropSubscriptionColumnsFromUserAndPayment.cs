using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropSubscriptionColumnsFromUserAndPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM [Notifications]
                WHERE [Type] IN (N'PlanExpiringSoon', N'PlanExpired');
                """);

            migrationBuilder.DropColumn(
                name: "PlanExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlanId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlanId",
                table: "PaymentTransactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PlanExpiresAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionPlanId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubscriptionPlanId",
                table: "PaymentTransactions",
                type: "uniqueidentifier",
                nullable: true);
        }
    }
}
