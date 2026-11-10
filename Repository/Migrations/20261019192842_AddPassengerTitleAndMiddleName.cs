using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddPassengerTitleAndMiddleName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "BookingPassengers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Passengers booked before titles were collected have no title; new bookings always carry one.
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "BookingPassengers",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "BookingPassengers");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "BookingPassengers");
        }
    }
}
