using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleFaresAndStops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlightScheduleFares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FlightScheduleId = table.Column<int>(type: "int", nullable: false),
                    CabinClass = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BasePrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightScheduleFares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightScheduleFares_FlightSchedules_FlightScheduleId",
                        column: x => x.FlightScheduleId,
                        principalTable: "FlightSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlightScheduleStops",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FlightScheduleId = table.Column<int>(type: "int", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    AirportId = table.Column<int>(type: "int", nullable: false),
                    ArrivalOffsetMinutes = table.Column<int>(type: "int", nullable: false),
                    LayoverMinutes = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlightScheduleStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlightScheduleStops_Airports_AirportId",
                        column: x => x.AirportId,
                        principalTable: "Airports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FlightScheduleStops_FlightSchedules_FlightScheduleId",
                        column: x => x.FlightScheduleId,
                        principalTable: "FlightSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlightScheduleFares_FlightScheduleId_CabinClass",
                table: "FlightScheduleFares",
                columns: new[] { "FlightScheduleId", "CabinClass" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FlightScheduleStops_AirportId",
                table: "FlightScheduleStops",
                column: "AirportId");

            migrationBuilder.CreateIndex(
                name: "IX_FlightScheduleStops_FlightScheduleId_SequenceNo",
                table: "FlightScheduleStops",
                columns: new[] { "FlightScheduleId", "SequenceNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlightScheduleFares");

            migrationBuilder.DropTable(
                name: "FlightScheduleStops");
        }
    }
}
