using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingJourneyNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JourneyNo",
                table: "BookingFlights",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Existing bookings: the outbound flight and everything before the return are journey 1, the return flight of a round trip is journey 2.
            migrationBuilder.Sql(@"UPDATE bf SET bf.JourneyNo = 2
FROM BookingFlights bf INNER JOIN Bookings b ON b.Id = bf.BookingId
WHERE b.TripType = 'RoundTrip' AND bf.LegOrder = 2");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JourneyNo",
                table: "BookingFlights");
        }
    }
}
