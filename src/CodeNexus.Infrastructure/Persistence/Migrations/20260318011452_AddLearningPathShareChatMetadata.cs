using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningPathShareChatMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LearningPathShares_PathId",
                table: "LearningPathShares");

            migrationBuilder.AddColumn<Guid>(
                name: "LearningPathShareId",
                table: "DirectMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathShares_PathId_MentorId_StudentId",
                table: "LearningPathShares",
                columns: new[] { "PathId", "MentorId", "StudentId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_LearningPathShareId",
                table: "DirectMessages",
                column: "LearningPathShareId");

            migrationBuilder.AddForeignKey(
                name: "FK_DirectMessages_LearningPathShares_LearningPathShareId",
                table: "DirectMessages",
                column: "LearningPathShareId",
                principalTable: "LearningPathShares",
                principalColumn: "ShareId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DirectMessages_LearningPathShares_LearningPathShareId",
                table: "DirectMessages");

            migrationBuilder.DropIndex(
                name: "IX_LearningPathShares_PathId_MentorId_StudentId",
                table: "LearningPathShares");

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_LearningPathShareId",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "LearningPathShareId",
                table: "DirectMessages");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathShares_PathId",
                table: "LearningPathShares",
                column: "PathId");
        }
    }
}
