using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddAirportTimeZone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Airports that already exist get UTC, a valid IANA zone, instead of an empty string.
            // Their real zones must be set before flights are generated from local schedule times.
            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Airports",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "Etc/UTC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Airports");
        }
    }
}
