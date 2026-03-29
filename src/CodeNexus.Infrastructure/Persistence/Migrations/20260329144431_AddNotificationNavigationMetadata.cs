using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationNavigationMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channels",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Web");

            migrationBuilder.AddColumn<Guid>(
                name: "ChapterId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LearningPathId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LessonId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "Info");

            migrationBuilder.AddColumn<Guid>(
                name: "TargetId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetUrl",
                table: "Notifications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TaskId",
                table: "Notifications",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Channels",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "ChapterId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LearningPathId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LessonId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Route",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TargetId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TargetUrl",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TaskId",
                table: "Notifications");
        }
    }
}
