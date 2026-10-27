using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EmailAttempts",
                table: "Notifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailSentAt",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            // The notifications that exist now were never meant to be e-mailed: they count as handled, so only news from now on goes out.
            migrationBuilder.Sql("UPDATE [Notifications] SET [EmailSentAt] = SYSUTCDATETIME() WHERE [EmailSentAt] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EmailSentAt",
                table: "Notifications",
                column: "EmailSentAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_EmailSentAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EmailAttempts",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EmailSentAt",
                table: "Notifications");
        }
    }
}
