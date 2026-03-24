using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorAiAccessPoliciesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MentorAiAccessPolicies",
                columns: table => new
                {
                    MentorAiAccessPolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorPaidRequestsMonthlyLimit = table.Column<int>(type: "int", nullable: false),
                    MentorDowngradeNotifyCooldownHours = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorAiAccessPolicies", x => x.MentorAiAccessPolicyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorAiAccessPolicies_UpdatedAt",
                table: "MentorAiAccessPolicies",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorAiAccessPolicies");
        }
    }
}
