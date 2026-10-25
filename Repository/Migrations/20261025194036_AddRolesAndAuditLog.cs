using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesAndAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActorUserId = table.Column<int>(type: "int", nullable: true),
                    ActorRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Details = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            // The customer role is now called User. Only the name changes; accounts, bookings and passwords are not touched.
            migrationBuilder.Sql("UPDATE [Users] SET [Role] = N'User' WHERE [Role] = N'Client';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Back to the two old roles: a SuperAdmin becomes an Admin, a User a Client.
            migrationBuilder.Sql("UPDATE [Users] SET [Role] = N'Admin' WHERE [Role] = N'SuperAdmin';");
            migrationBuilder.Sql("UPDATE [Users] SET [Role] = N'Client' WHERE [Role] = N'User';");

            migrationBuilder.DropTable(
                name: "AuditLogs");
        }
    }
}
