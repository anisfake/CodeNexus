using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLearnProgressTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "SubscriptionPlanId",
                keyValue: new Guid("8be8f5d9-9c78-4d96-8d0f-2a8a3dcfa9fb"));

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "SubscriptionPlanId",
                keyValue: new Guid("b06ea0a8-d6d1-4cc4-9ef0-b53659956d11"));

            migrationBuilder.DeleteData(
                table: "SubscriptionPlans",
                keyColumn: "SubscriptionPlanId",
                keyValue: new Guid("cd0c6d3b-a7e7-48e5-a746-d6e16f9f39d4"));

            migrationBuilder.CreateTable(
                name: "LearnProgress",
                columns: table => new
                {
                    ProgressId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LessonId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsLessonContentRead = table.Column<bool>(type: "bit", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearnProgress", x => x.ProgressId);
                    table.ForeignKey(
                        name: "FK_LearnProgress_Lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "Lessons",
                        principalColumn: "LessonId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearnProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearnProgress_LessonId_UserId",
                table: "LearnProgress",
                columns: new[] { "LessonId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LearnProgress_UserId",
                table: "LearnProgress",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearnProgress");

            migrationBuilder.InsertData(
                table: "SubscriptionPlans",
                columns: new[] { "SubscriptionPlanId", "Description", "DisplayOrder", "DurationDays", "IsActive", "Name", "PlanType", "PriceVnd" },
                values: new object[,]
                {
                    { new Guid("8be8f5d9-9c78-4d96-8d0f-2a8a3dcfa9fb"), "Mo khoa personal goals va model tra phi cho hoc tap ca nhan.", 2, 30, true, "Standard", "Standard", 99000m },
                    { new Guid("b06ea0a8-d6d1-4cc4-9ef0-b53659956d11"), "Dung model free va chi duoc su dung system goals.", 1, 0, true, "Free", "Free", 0m },
                    { new Guid("cd0c6d3b-a7e7-48e5-a746-d6e16f9f39d4"), "Toan bo quyen Standard voi thoi han dai hon va uu tien tinh nang AI nang cao.", 3, 90, true, "Pro", "Pro", 249000m }
                });
        }
    }
}
