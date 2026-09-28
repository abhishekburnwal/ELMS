using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceHoursAndProject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "AttendanceRecords",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WorkingHours",
                table: "AttendanceRecords",
                type: "decimal(4,2)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEOivAcFMIVCB3IlM32YN5ogUfsjsWRSz8kVVyZRycCLob/pfp1bdmwuChHSn9UPxJg==");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEPnuIrjXJwpHw7LM+E7SkS/E25Sc2e4/szkAY6Q7QGSvBBwXYytuXLfKQznwnFbflg==");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "WorkingHours",
                table: "AttendanceRecords");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEO4TFS5OIWiWM3JAZyt8xyzZ9SbBs/6MVkd2NdKFZdAjZpfiqGwDwpxQQlmHq8TBsg==");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEFdgch/B4xlcZG8dAWisaa9BnwHiqzT9atqtpgIEM2XJGXDeSosHJL2p+H5B3oJtWg==");
        }
    }
}
