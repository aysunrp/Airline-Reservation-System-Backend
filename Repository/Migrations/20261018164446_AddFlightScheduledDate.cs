using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddFlightScheduledDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Flights_FlightScheduleId",
                table: "Flights");

            migrationBuilder.AddColumn<DateOnly>(
                name: "ScheduledDate",
                table: "Flights",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Flights_FlightScheduleId_ScheduledDate",
                table: "Flights",
                columns: new[] { "FlightScheduleId", "ScheduledDate" },
                unique: true,
                filter: "[FlightScheduleId] IS NOT NULL AND [ScheduledDate] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Flights_FlightScheduleId_ScheduledDate",
                table: "Flights");

            migrationBuilder.DropColumn(
                name: "ScheduledDate",
                table: "Flights");

            migrationBuilder.CreateIndex(
                name: "IX_Flights_FlightScheduleId",
                table: "Flights",
                column: "FlightScheduleId");
        }
    }
}
