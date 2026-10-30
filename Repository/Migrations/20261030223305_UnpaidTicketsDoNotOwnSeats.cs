using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class UnpaidTicketsDoNotOwnSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_FlightSeatId",
                table: "Tickets");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_FlightSeatId",
                table: "Tickets",
                column: "FlightSeatId",
                unique: true,
                filter: "[FlightSeatId] IS NOT NULL AND [Status] <> 'Cancelled' AND [Status] <> 'Pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_FlightSeatId",
                table: "Tickets");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_FlightSeatId",
                table: "Tickets",
                column: "FlightSeatId",
                unique: true,
                filter: "[FlightSeatId] IS NOT NULL AND [Status] <> 'Cancelled'");
        }
    }
}
