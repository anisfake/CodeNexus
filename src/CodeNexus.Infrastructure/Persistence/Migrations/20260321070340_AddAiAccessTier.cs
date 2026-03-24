using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAccessTier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AIProviderConfigs_UsageType_IsActive",
                table: "AIProviderConfigs");

            migrationBuilder.AddColumn<string>(
                name: "AccessTier",
                table: "AIProviderConfigs",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AIProviderConfigs_UsageType_AccessTier",
                table: "AIProviderConfigs",
                columns: new[] { "UsageType", "AccessTier" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_AIProviderConfigs_UsageType_AccessTier_IsActive",
                table: "AIProviderConfigs",
                columns: new[] { "UsageType", "AccessTier", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AIProviderConfigs_UsageType_AccessTier",
                table: "AIProviderConfigs");

            migrationBuilder.DropIndex(
                name: "IX_AIProviderConfigs_UsageType_AccessTier_IsActive",
                table: "AIProviderConfigs");

            migrationBuilder.DropColumn(
                name: "AccessTier",
                table: "AIProviderConfigs");

            migrationBuilder.CreateIndex(
                name: "IX_AIProviderConfigs_UsageType_IsActive",
                table: "AIProviderConfigs",
                columns: new[] { "UsageType", "IsActive" });
        }
    }
}
