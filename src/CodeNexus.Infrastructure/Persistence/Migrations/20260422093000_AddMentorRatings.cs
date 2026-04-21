using System;
using CodeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260422093000_AddMentorRatings")]
    public partial class AddMentorRatings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MentorRatings",
                columns: table => new
                {
                    RatingId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorRatings", x => x.RatingId);
                    table.ForeignKey(
                        name: "FK_MentorRatings_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_MentorRatings_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.NoAction);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorRatings_MentorId_CreatedAt",
                table: "MentorRatings",
                columns: new[] { "MentorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MentorRatings_MentorId_StudentId",
                table: "MentorRatings",
                columns: new[] { "MentorId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorRatings_StudentId",
                table: "MentorRatings",
                column: "StudentId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorRatings");
        }
    }
}
