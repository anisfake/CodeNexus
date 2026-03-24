using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMutilpleGoal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningPaths_Goals_GoalId",
                table: "LearningPaths");

            migrationBuilder.DropIndex(
                name: "IX_LearningPaths_GoalId",
                table: "LearningPaths");

            migrationBuilder.DropColumn(
                name: "GoalId",
                table: "LearningPaths");

            migrationBuilder.CreateTable(
                name: "LearningPathGoals",
                columns: table => new
                {
                    PathId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPathGoals", x => new { x.PathId, x.GoalId });
                    table.ForeignKey(
                        name: "FK_LearningPathGoals_Goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "GoalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningPathGoals_LearningPaths_PathId",
                        column: x => x.PathId,
                        principalTable: "LearningPaths",
                        principalColumn: "PathId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathGoals_GoalId",
                table: "LearningPathGoals",
                column: "GoalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningPathGoals");

            migrationBuilder.AddColumn<Guid>(
                name: "GoalId",
                table: "LearningPaths",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningPaths_GoalId",
                table: "LearningPaths",
                column: "GoalId");

            migrationBuilder.AddForeignKey(
                name: "FK_LearningPaths_Goals_GoalId",
                table: "LearningPaths",
                column: "GoalId",
                principalTable: "Goals",
                principalColumn: "GoalId");
        }
    }
}
