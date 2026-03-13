using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeNexus.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateExistingFocusSessionData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update existing FocusSession records to set proper PlannedDurationMinutes and SessionStatus
            migrationBuilder.Sql(@"
                UPDATE FocusSessions 
                SET PlannedDurationMinutes = 25 
                WHERE PlannedDurationMinutes IS NULL OR PlannedDurationMinutes = 0;
            ");

            // Set SessionStatus for completed sessions (those with EndTime)
            migrationBuilder.Sql(@"
                UPDATE FocusSessions 
                SET SessionStatus = 2 
                WHERE EndTime IS NOT NULL AND SessionStatus = 0;
            ");

            // Set SessionStatus for abandoned sessions (those without EndTime but created more than 2 hours ago)
            migrationBuilder.Sql(@"
                UPDATE FocusSessions 
                SET SessionStatus = 3 
                WHERE EndTime IS NULL 
                  AND SessionStatus = 0 
                  AND DATEDIFF(HOUR, StartTime, GETUTCDATE()) > 2;
            ");

            // Calculate ActualDurationMinutes for sessions that have EndTime but no ActualDurationMinutes
            migrationBuilder.Sql(@"
                UPDATE FocusSessions 
                SET ActualDurationMinutes = DATEDIFF(MINUTE, StartTime, EndTime)
                WHERE EndTime IS NOT NULL 
                  AND (ActualDurationMinutes IS NULL OR ActualDurationMinutes = 0);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert changes - set all SessionStatus back to Running (0) and clear calculated values
            migrationBuilder.Sql(@"
                UPDATE FocusSessions 
                SET SessionStatus = 0, 
                    ActualDurationMinutes = NULL 
                WHERE SessionStatus IN (2, 3);
            ");

            // Optionally revert PlannedDurationMinutes to NULL (though this might not be desired)
            // migrationBuilder.Sql(@"
            //     UPDATE FocusSessions 
            //     SET PlannedDurationMinutes = NULL;
            // ");
        }
    }
}
