using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveValidationRequestTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningPathValidationRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LearningPathValidationRequests",
                columns: table => new
                {
                    ValidationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MentorFeedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPathValidationRequests", x => x.ValidationRequestId);
                    table.ForeignKey(
                        name: "FK_LearningPathValidationRequests_LearningPaths_PathId",
                        column: x => x.PathId,
                        principalTable: "LearningPaths",
                        principalColumn: "PathId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningPathValidationRequests_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningPathValidationRequests_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });
        }
    }
}
