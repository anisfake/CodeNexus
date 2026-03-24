using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenPricingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TokenPricings",
                columns: table => new
                {
                    TokenPricingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TokenAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriceVnd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokenPricings");
        }
    }
}
