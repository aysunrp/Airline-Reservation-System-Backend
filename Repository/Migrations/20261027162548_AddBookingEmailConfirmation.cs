using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingEmailConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookingId",
                table: "EmailTokens",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContactConfirmedAt",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailTokens_BookingId",
                table: "EmailTokens",
                column: "BookingId");

            // Bookings made before the rule were never asked for a link: they count as confirmed, so none of them is stuck before payment.
            migrationBuilder.Sql("UPDATE [Bookings] SET [ContactConfirmedAt] = [CreatedAt] WHERE [ContactConfirmedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EmailTokens_BookingId",
                table: "EmailTokens");

            migrationBuilder.DropColumn(
                name: "BookingId",
                table: "EmailTokens");

            migrationBuilder.DropColumn(
                name: "ContactConfirmedAt",
                table: "Bookings");
        }
    }
}
