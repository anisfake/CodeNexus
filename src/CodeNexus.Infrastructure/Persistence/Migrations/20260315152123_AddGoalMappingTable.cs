using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalMappingTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GoalMappings",
                columns: table => new
                {
                    MappingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    VerifiedByAI = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalMappings", x => x.MappingId);
                    table.ForeignKey(
                        name: "FK_GoalMappings_Goals_SystemGoalId",
                        column: x => x.SystemGoalId,
                        principalTable: "Goals",
                        principalColumn: "GoalId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GoalMappings_Goals_UserGoalId",
                        column: x => x.UserGoalId,
                        principalTable: "Goals",
                        principalColumn: "GoalId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GoalMappings_SystemGoalId",
                table: "GoalMappings",
                column: "SystemGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalMappings_UserGoalId_SystemGoalId",
                table: "GoalMappings",
                columns: new[] { "UserGoalId", "SystemGoalId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GoalMappings");
        }
    }
}
