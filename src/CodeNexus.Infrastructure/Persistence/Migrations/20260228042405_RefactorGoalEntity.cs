using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorGoalEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goals_Users_UserId",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_Goals_UserId",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "DurationDays",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Goals");

            migrationBuilder.RenameColumn(
                name: "IsCompleted",
                table: "Goals",
                newName: "IsSystemDefined");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "Goals",
                newName: "UpdatedAt");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "Goals",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Goals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Goals_CreatedByUserId",
                table: "Goals",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Goals_Users_CreatedByUserId",
                table: "Goals",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goals_Users_CreatedByUserId",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_Goals_CreatedByUserId",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Goals");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "Goals",
                newName: "CompletedAt");

            migrationBuilder.RenameColumn(
                name: "IsSystemDefined",
                table: "Goals",
                newName: "IsCompleted");

            migrationBuilder.AddColumn<int>(
                name: "DurationDays",
                table: "Goals",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Goals",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Goals_UserId",
                table: "Goals",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Goals_Users_UserId",
                table: "Goals",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
