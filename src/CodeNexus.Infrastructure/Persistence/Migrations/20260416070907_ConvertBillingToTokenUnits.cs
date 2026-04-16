using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvertBillingToTokenUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BalanceVnd",
                table: "Users",
                newName: "TokenBalance");

            migrationBuilder.RenameColumn(
                name: "CreditedBalanceVnd",
                table: "TokenPackages",
                newName: "CreditedTokens");

            migrationBuilder.RenameColumn(
                name: "CreditedAmountVnd",
                table: "PaymentTransactions",
                newName: "CreditedTokens");

            migrationBuilder.RenameColumn(
                name: "CostUsd",
                table: "AIUsageLogs",
                newName: "ChargedTokens");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TokenBalance",
                table: "Users",
                newName: "BalanceVnd");

            migrationBuilder.RenameColumn(
                name: "CreditedTokens",
                table: "TokenPackages",
                newName: "CreditedBalanceVnd");

            migrationBuilder.RenameColumn(
                name: "CreditedTokens",
                table: "PaymentTransactions",
                newName: "CreditedAmountVnd");

            migrationBuilder.RenameColumn(
                name: "ChargedTokens",
                table: "AIUsageLogs",
                newName: "CostUsd");
        }
    }
}
