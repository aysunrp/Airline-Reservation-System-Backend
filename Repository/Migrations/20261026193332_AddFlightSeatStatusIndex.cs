using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightSeatStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_FlightSeats_Status_FlightId_HeldUntil",
                table: "FlightSeats",
                columns: new[] { "Status", "FlightId", "HeldUntil" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FlightSeats_Status_FlightId_HeldUntil",
                table: "FlightSeats");
        }
    }
}
