using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateTaskGoalsAndFocusGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FocusGoals");

            migrationBuilder.DropTable(
                name: "TaskGoals");

            migrationBuilder.AddColumn<string>(
                name: "GoalContent",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GoalDeadline",
                table: "Tasks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoalContent",
                table: "FocusSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GoalDeadline",
                table: "FocusSessions",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GoalContent",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "GoalDeadline",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "GoalContent",
                table: "FocusSessions");

            migrationBuilder.DropColumn(
                name: "GoalDeadline",
                table: "FocusSessions");

            migrationBuilder.CreateTable(
                name: "FocusGoals",
                columns: table => new
                {
                    FocusGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FocusGoals", x => x.FocusGoalId);
                    table.ForeignKey(
                        name: "FK_FocusGoals_FocusSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "FocusSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskGoals",
                columns: table => new
                {
                    TaskGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskGoals", x => x.TaskGoalId);
                    table.ForeignKey(
                        name: "FK_TaskGoals_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "TaskId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FocusGoals_SessionId",
                table: "FocusGoals",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskGoals_TaskId",
                table: "TaskGoals",
                column: "TaskId",
                unique: true);
        }
    }
}
