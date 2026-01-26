using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSubjectResourceLearningPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_Users_UserId",
                table: "Subjects");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Subjects",
                newName: "CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Subjects_UserId",
                table: "Subjects",
                newName: "IX_Subjects_CreatedByUserId");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Resources",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "LearningPaths",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Resources_UserId",
                table: "Resources",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPaths_UserId",
                table: "LearningPaths",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_LearningPaths_Users_UserId",
                table: "LearningPaths",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Resources_Users_UserId",
                table: "Resources",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_Users_CreatedByUserId",
                table: "Subjects",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningPaths_Users_UserId",
                table: "LearningPaths");

            migrationBuilder.DropForeignKey(
                name: "FK_Resources_Users_UserId",
                table: "Resources");

            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_Users_CreatedByUserId",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_Resources_UserId",
                table: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_LearningPaths_UserId",
                table: "LearningPaths");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "LearningPaths");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserId",
                table: "Subjects",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Subjects_CreatedByUserId",
                table: "Subjects",
                newName: "IX_Subjects_UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_Users_UserId",
                table: "Subjects",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
