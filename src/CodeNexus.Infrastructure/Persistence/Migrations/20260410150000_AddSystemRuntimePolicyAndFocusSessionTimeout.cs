using System;
using CodeNexus.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260410150000_AddSystemRuntimePolicyAndFocusSessionTimeout")]
    public partial class AddSystemRuntimePolicyAndFocusSessionTimeout : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "FocusSessions",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.Sql(@"
                UPDATE fs
                SET LastActivityAt = COALESCE(fs.PausedAt, fs.EndTime, fs.StartTime, fs.CreatedAt, GETUTCDATE())
                FROM FocusSessions fs
            ");

            migrationBuilder.CreateTable(
                name: "SystemRuntimePolicies",
                columns: table => new
                {
                    SystemRuntimePolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemRuntimePolicies", x => x.SystemRuntimePolicyId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_SessionStatus_LastActivityAt",
                table: "FocusSessions",
                columns: new[] { "SessionStatus", "LastActivityAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemRuntimePolicies_UpdatedAt",
                table: "SystemRuntimePolicies",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SystemRuntimePolicies_PolicyKey",
                table: "SystemRuntimePolicies",
                column: "PolicyKey",
                unique: true);

            migrationBuilder.Sql(@"
                INSERT INTO SystemRuntimePolicies
                    (SystemRuntimePolicyId, PolicyKey, Description, ConfigJson, IsActive, UpdatedAt)
                VALUES
                    (NEWID(), 'runtime_policy', 'Default runtime operation policy',
                     N'{""focusSessionAutoPauseAfterMinutes"":10,""focusSessionAutoAbandonAfterMinutes"":720,""focusSessionMonitorIntervalSeconds"":60,""pendingPaymentTimeoutMinutes"":15,""pendingPaymentMonitorIntervalSeconds"":60,""overdueNotificationIntervalMinutes"":15}',
                     1, GETUTCDATE())
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemRuntimePolicies");

            migrationBuilder.DropIndex(
                name: "IX_FocusSessions_SessionStatus_LastActivityAt",
                table: "FocusSessions");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "FocusSessions");
        }
    }
}
