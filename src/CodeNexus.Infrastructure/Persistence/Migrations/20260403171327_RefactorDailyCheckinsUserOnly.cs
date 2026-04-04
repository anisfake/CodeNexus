using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorDailyCheckinsUserOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyCheckins_FocusSessions_SessionId",
                table: "DailyCheckins");

            migrationBuilder.DropIndex(
                name: "IX_DailyCheckins_SessionId",
                table: "DailyCheckins");

            migrationBuilder.DropIndex(
                name: "IX_DailyCheckins_SessionId_CheckinDate",
                table: "DailyCheckins");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "DailyCheckins",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE dc
                SET dc.UserId = lp.UserId
                FROM DailyCheckins dc
                INNER JOIN FocusSessions fs ON dc.SessionId = fs.SessionId
                INNER JOIN Tasks t ON fs.TaskId = t.TaskId
                INNER JOIN LearningPaths lp ON t.PathId = lp.PathId;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "DailyCheckins",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "DailyCheckins");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCheckins_UserId_CheckinDate",
                table: "DailyCheckins",
                columns: new[] { "UserId", "CheckinDate" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCheckins_Users_UserId",
                table: "DailyCheckins",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DailyCheckins_Users_UserId",
                table: "DailyCheckins");

            migrationBuilder.DropIndex(
                name: "IX_DailyCheckins_UserId_CheckinDate",
                table: "DailyCheckins");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "DailyCheckins",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE dc
                SET dc.SessionId = src.SessionId
                FROM DailyCheckins dc
                OUTER APPLY (
                    SELECT TOP 1 fs.SessionId
                    FROM FocusSessions fs
                    INNER JOIN Tasks t ON fs.TaskId = t.TaskId
                    INNER JOIN LearningPaths lp ON t.PathId = lp.PathId
                    WHERE lp.UserId = dc.UserId
                    ORDER BY fs.CreatedAt DESC
                ) src;

                DELETE FROM DailyCheckins WHERE SessionId IS NULL;
            ");

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                table: "DailyCheckins",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "DailyCheckins");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCheckins_SessionId_CheckinDate",
                table: "DailyCheckins",
                columns: new[] { "SessionId", "CheckinDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyCheckins_SessionId",
                table: "DailyCheckins",
                column: "SessionId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyCheckins_FocusSessions_SessionId",
                table: "DailyCheckins",
                column: "SessionId",
                principalTable: "FocusSessions",
                principalColumn: "SessionId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
