using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningPathShareVersionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AcceptedPathId",
                table: "LearningPathShares",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IgnoredSourceVersion",
                table: "LearningPathShares",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvalidatedReason",
                table: "LearningPathShares",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTrackingEnabled",
                table: "LearningPathShares",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "LastNotifiedSourceVersion",
                table: "LearningPathShares",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceVersionAtAccept",
                table: "LearningPathShares",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VersionNumber",
                table: "LearningPaths",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathShares_AcceptedPathId",
                table: "LearningPathShares",
                column: "AcceptedPathId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathShares_PathId_StudentId_Status_IsTrackingEnabled",
                table: "LearningPathShares",
                columns: new[] { "PathId", "StudentId", "Status", "IsTrackingEnabled" });

            migrationBuilder.AddForeignKey(
                name: "FK_LearningPathShares_LearningPaths_AcceptedPathId",
                table: "LearningPathShares",
                column: "AcceptedPathId",
                principalTable: "LearningPaths",
                principalColumn: "PathId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningPathShares_LearningPaths_AcceptedPathId",
                table: "LearningPathShares");

            migrationBuilder.DropIndex(
                name: "IX_LearningPathShares_AcceptedPathId",
                table: "LearningPathShares");

            migrationBuilder.DropIndex(
                name: "IX_LearningPathShares_PathId_StudentId_Status_IsTrackingEnabled",
                table: "LearningPathShares");

            migrationBuilder.DropColumn(
                name: "AcceptedPathId",
                table: "LearningPathShares");

            migrationBuilder.DropColumn(
                name: "IgnoredSourceVersion",
                table: "LearningPathShares");

            migrationBuilder.DropColumn(
                name: "InvalidatedReason",
                table: "LearningPathShares");

            migrationBuilder.DropColumn(
                name: "IsTrackingEnabled",
                table: "LearningPathShares");

            migrationBuilder.DropColumn(
                name: "LastNotifiedSourceVersion",
                table: "LearningPathShares");

            migrationBuilder.DropColumn(
                name: "SourceVersionAtAccept",
                table: "LearningPathShares");

            migrationBuilder.DropColumn(
                name: "VersionNumber",
                table: "LearningPaths");
        }
    }
}
