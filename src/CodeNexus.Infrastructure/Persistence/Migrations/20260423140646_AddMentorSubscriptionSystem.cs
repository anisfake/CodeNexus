using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMentorSubscriptionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MentorPackageId",
                table: "PaymentTransactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LearningPathValidationRequests",
                columns: table => new
                {
                    ValidationRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PathId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StudentNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MentorFeedback = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningPathValidationRequests", x => x.ValidationRequestId);
                    table.ForeignKey(
                        name: "FK_LearningPathValidationRequests_LearningPaths_PathId",
                        column: x => x.PathId,
                        principalTable: "LearningPaths",
                        principalColumn: "PathId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningPathValidationRequests_Users_MentorId",
                        column: x => x.MentorId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LearningPathValidationRequests_Users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MentorPackages",
                columns: table => new
                {
                    MentorPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PriceVnd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SharesFromMentorLimit = table.Column<int>(type: "int", nullable: false),
                    ValidationRequestLimit = table.Column<int>(type: "int", nullable: false),
                    TaskReviewLimit = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorPackages", x => x.MentorPackageId);
                });

            migrationBuilder.CreateTable(
                name: "StudentMentorSubscriptions",
                columns: table => new
                {
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentorPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SharesFromMentorLimit = table.Column<int>(type: "int", nullable: false),
                    SharesFromMentorUsed = table.Column<int>(type: "int", nullable: false),
                    ValidationRequestLimit = table.Column<int>(type: "int", nullable: false),
                    ValidationRequestsUsed = table.Column<int>(type: "int", nullable: false),
                    TaskReviewLimit = table.Column<int>(type: "int", nullable: false),
                    TaskReviewsUsed = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentMentorSubscriptions", x => x.SubscriptionId);
                    table.ForeignKey(
                        name: "FK_StudentMentorSubscriptions_MentorPackages_MentorPackageId",
                        column: x => x.MentorPackageId,
                        principalTable: "MentorPackages",
                        principalColumn: "MentorPackageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StudentMentorSubscriptions_PaymentTransactions_PaymentTransactionId",
                        column: x => x.PaymentTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "PaymentTransactionId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StudentMentorSubscriptions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_MentorPackageId",
                table: "PaymentTransactions",
                column: "MentorPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathValidationRequests_MentorId",
                table: "LearningPathValidationRequests",
                column: "MentorId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathValidationRequests_PathId",
                table: "LearningPathValidationRequests",
                column: "PathId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningPathValidationRequests_StudentId",
                table: "LearningPathValidationRequests",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMentorSubscriptions_MentorPackageId",
                table: "StudentMentorSubscriptions",
                column: "MentorPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMentorSubscriptions_PaymentTransactionId",
                table: "StudentMentorSubscriptions",
                column: "PaymentTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentMentorSubscriptions_UserId",
                table: "StudentMentorSubscriptions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_MentorPackages_MentorPackageId",
                table: "PaymentTransactions",
                column: "MentorPackageId",
                principalTable: "MentorPackages",
                principalColumn: "MentorPackageId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_MentorPackages_MentorPackageId",
                table: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "LearningPathValidationRequests");

            migrationBuilder.DropTable(
                name: "StudentMentorSubscriptions");

            migrationBuilder.DropTable(
                name: "MentorPackages");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_MentorPackageId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "MentorPackageId",
                table: "PaymentTransactions");
        }
    }
}
