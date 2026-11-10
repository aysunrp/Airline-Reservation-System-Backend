using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Refunds_CancellationId",
                table: "Refunds");

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "Refunds",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Refunds",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Refunds",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InitiatedByUserId",
                table: "Refunds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderStatus",
                table: "Refunds",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Refunds",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_CancellationId_PaymentId",
                table: "Refunds",
                columns: new[] { "CancellationId", "PaymentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_InitiatedByUserId",
                table: "Refunds",
                column: "InitiatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_ProviderReference",
                table: "Refunds",
                column: "ProviderReference",
                unique: true,
                filter: "[ProviderReference] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Refunds_Users_InitiatedByUserId",
                table: "Refunds",
                column: "InitiatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Refunds_Users_InitiatedByUserId",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_CancellationId_PaymentId",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_InitiatedByUserId",
                table: "Refunds");

            migrationBuilder.DropIndex(
                name: "IX_Refunds_ProviderReference",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "InitiatedByUserId",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "ProviderStatus",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Refunds");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_CancellationId",
                table: "Refunds",
                column: "CancellationId");
        }
    }
}
