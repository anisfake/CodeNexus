using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGoalProgressTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserGoalProgresses",
                columns: table => new
                {
                    UserGoalProgressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LearningPathId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGoalProgresses", x => x.UserGoalProgressId);
                    table.ForeignKey(
                        name: "FK_UserGoalProgresses_Goals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goals",
                        principalColumn: "GoalId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGoalProgresses_LearningPaths_LearningPathId",
                        column: x => x.LearningPathId,
                        principalTable: "LearningPaths",
                        principalColumn: "PathId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserGoalProgresses_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserGoalProgresses_GoalId",
                table: "UserGoalProgresses",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGoalProgresses_LearningPathId",
                table: "UserGoalProgresses",
                column: "LearningPathId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGoalProgresses_UserId_GoalId_LearningPathId",
                table: "UserGoalProgresses",
                columns: new[] { "UserId", "GoalId", "LearningPathId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGoalProgresses_UserId_LastUpdatedAt",
                table: "UserGoalProgresses",
                columns: new[] { "UserId", "LastUpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserGoalProgresses");
        }
    }
}
