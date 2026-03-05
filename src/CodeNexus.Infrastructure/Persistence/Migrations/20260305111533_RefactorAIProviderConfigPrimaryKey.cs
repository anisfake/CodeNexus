using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAIProviderConfigPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AIProviderConfigs_ProviderName",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ProviderName",
                table: "Conversations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AIProviderConfigs",
                table: "AIProviderConfigs");

            migrationBuilder.DropColumn(
                name: "ProviderName",
                table: "Conversations");

            migrationBuilder.RenameColumn(
                name: "IsEnabled",
                table: "AIProviderConfigs",
                newName: "IsActive");

            migrationBuilder.AddColumn<Guid>(
                name: "ConfigId",
                table: "Conversations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AlterColumn<string>(
                name: "ProviderName",
                table: "AIProviderConfigs",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<Guid>(
                name: "ConfigId",
                table: "AIProviderConfigs",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Generate unique GUIDs for existing records
            migrationBuilder.Sql(@"
                UPDATE AIProviderConfigs 
                SET ConfigId = NEWID()
            ");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AIProviderConfigs",
                table: "AIProviderConfigs",
                column: "ConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ConfigId",
                table: "Conversations",
                column: "ConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_AIProviderConfigs_UsageType_IsActive",
                table: "AIProviderConfigs",
                columns: new[] { "UsageType", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AIProviderConfigs_ConfigId",
                table: "Conversations",
                column: "ConfigId",
                principalTable: "AIProviderConfigs",
                principalColumn: "ConfigId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Conversations_AIProviderConfigs_ConfigId",
                table: "Conversations");

            migrationBuilder.DropIndex(
                name: "IX_Conversations_ConfigId",
                table: "Conversations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_AIProviderConfigs",
                table: "AIProviderConfigs");

            migrationBuilder.DropIndex(
                name: "IX_AIProviderConfigs_UsageType_IsActive",
                table: "AIProviderConfigs");

            migrationBuilder.DropColumn(
                name: "ConfigId",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "ConfigId",
                table: "AIProviderConfigs");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "AIProviderConfigs",
                newName: "IsEnabled");

            migrationBuilder.AddColumn<string>(
                name: "ProviderName",
                table: "Conversations",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ProviderName",
                table: "AIProviderConfigs",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_AIProviderConfigs",
                table: "AIProviderConfigs",
                column: "ProviderName");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_ProviderName",
                table: "Conversations",
                column: "ProviderName");

            migrationBuilder.AddForeignKey(
                name: "FK_Conversations_AIProviderConfigs_ProviderName",
                table: "Conversations",
                column: "ProviderName",
                principalTable: "AIProviderConfigs",
                principalColumn: "ProviderName",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
