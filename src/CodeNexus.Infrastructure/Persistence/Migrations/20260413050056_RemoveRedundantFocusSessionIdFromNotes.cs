using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRedundantFocusSessionIdFromNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE n
                SET n.SessionId = n.FocusSessionSessionId
                FROM Notes n
                WHERE n.SessionId IS NULL
                  AND n.FocusSessionSessionId IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Notes_FocusSessions_FocusSessionSessionId",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_FocusSessionSessionId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "FocusSessionSessionId",
                table: "Notes");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_SessionId",
                table: "Notes",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_FocusSessions_SessionId",
                table: "Notes",
                column: "SessionId",
                principalTable: "FocusSessions",
                principalColumn: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notes_FocusSessions_SessionId",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_SessionId",
                table: "Notes");

            migrationBuilder.AddColumn<Guid>(
                name: "FocusSessionSessionId",
                table: "Notes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE n
                SET n.FocusSessionSessionId = n.SessionId
                FROM Notes n
                WHERE n.FocusSessionSessionId IS NULL
                  AND n.SessionId IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Notes_FocusSessionSessionId",
                table: "Notes",
                column: "FocusSessionSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_FocusSessions_FocusSessionSessionId",
                table: "Notes",
                column: "FocusSessionSessionId",
                principalTable: "FocusSessions",
                principalColumn: "SessionId");
        }
    }
}
