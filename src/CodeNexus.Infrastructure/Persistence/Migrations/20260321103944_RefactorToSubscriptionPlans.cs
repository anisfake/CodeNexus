using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorToSubscriptionPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokenPricings");

            migrationBuilder.DropColumn(
                name: "TokenBalance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TokenAmount",
                table: "PaymentTransactions");

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

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PriceVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DurationDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.SubscriptionPlanId);
                });

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "SubscriptionPlanId", "Description", "DisplayOrder", "DurationDays", "IsActive", "Name", "PlanType", "PriceVnd" },
                values: new object[,]
                {
                    { new Guid("8be8f5d9-9c78-4d96-8d0f-2a8a3dcfa9fb"), "Mo khoa personal goals va model tra phi cho hoc tap ca nhan.", 2, 30, true, "Standard", "Standard", 99000m },
                    { new Guid("b06ea0a8-d6d1-4cc4-9ef0-b53659956d11"), "Dung model free va chi duoc su dung system goals.", 1, 0, true, "Free", "Free", 0m },
                    { new Guid("cd0c6d3b-a7e7-48e5-a746-d6e16f9f39d4"), "Toan bo quyen Standard voi thoi han dai hon va uu tien tinh nang AI nang cao.", 3, 90, true, "Pro", "Pro", 249000m }
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_SubscriptionPlans_SubscriptionPlanId",
                table: "PaymentTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_SubscriptionPlans_SubscriptionPlanId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropIndex(
                name: "IX_Users_SubscriptionPlanId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_SubscriptionPlanId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PlanExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlanId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubscriptionPlanId",
                table: "PaymentTransactions");

            migrationBuilder.AddColumn<decimal>(
                name: "TokenBalance",
                table: "Users",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TokenAmount",
                table: "PaymentTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "TokenPricings",
                columns: table => new
                {
                    TokenPricingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PriceVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TokenAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenPricings", x => x.TokenPricingId);
                });

            migrationBuilder.InsertData(
                table: "TokenPricings",
                columns: new[] { "TokenPricingId", "CreatedAt", "Description", "DisplayOrder", "IsActive", "Name", "PriceVnd", "TokenAmount", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("1271d5d0-7eec-4fa4-9f2b-e9d33f8927d5"), new DateTime(2026, 3, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Tiet kiem 10% so voi goi Starter.", 2, true, "Standard 300", 27000m, 300m, null },
                    { new Guid("ad7f249c-a4f6-4b7e-a64c-8e59fda5d5ad"), new DateTime(2026, 3, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Tiet kiem 40%, danh cho nguoi dung nap so luong lon.", 5, true, "Ultra 3200", 192000m, 3200m, null },
                    { new Guid("b07eab68-d095-4f37-b5d2-0f153f41d5c1"), new DateTime(2026, 3, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Tiet kiem 20%, phu hop hoc lien tuc.", 3, true, "Pro 700", 56000m, 700m, null },
                    { new Guid("bd844af6-3bd2-4f70-bd44-6be5f89dc6f2"), new DateTime(2026, 3, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Goi nap co ban de dung model tra phi.", 1, true, "Starter 100", 10000m, 100m, null },
                    { new Guid("f28df893-98f2-41cf-9daa-b5acbf8b7a66"), new DateTime(2026, 3, 21, 0, 0, 0, 0, DateTimeKind.Utc), "Tiet kiem 30%, toi uu cho hoc theo lo trinh dai han.", 4, true, "Plus 1500", 105000m, 1500m, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokenPricings_DisplayOrder",
                table: "TokenPricings",
                column: "DisplayOrder");
        }
    }
}
