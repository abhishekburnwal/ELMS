using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "LeaveRequestId",
                table: "AuditLogs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "AttendanceRecordId",
                table: "AuditLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "AuditLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1,
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEM/O5hZXi/t5RVQTyBF6bOENVVj3wJ8xKztKFAFIyzrWkPezxt9KgIdl8UuDhXb2wA==");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 2,
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEOQCxCQkySUSCCTNTls26a1P4JjwJPcBzGW4I5oc9yNnwFIIoSRW796CfMHSuqFr3g==");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_AttendanceRecordId",
                table: "AuditLogs",
                column: "AttendanceRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_AttendanceRecords_AttendanceRecordId",
                table: "AuditLogs",
                column: "AttendanceRecordId",
                principalTable: "AttendanceRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_AttendanceRecords_AttendanceRecordId",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_AttendanceRecordId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "AttendanceRecordId",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<int>(
                name: "LeaveRequestId",
                table: "AuditLogs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

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
    }
}
