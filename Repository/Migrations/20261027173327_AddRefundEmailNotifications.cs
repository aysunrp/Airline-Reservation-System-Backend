using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundEmailNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmailKey",
                table: "Notifications",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Cancellations",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EmailKey",
                table: "Notifications",
                column: "EmailKey",
                unique: true,
                filter: "[EmailKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_EmailKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "EmailKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Cancellations");
        }
    }
}
