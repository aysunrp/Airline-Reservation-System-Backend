using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsletterSubscribers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NewsletterSubscribers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    ConfirmationTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ConfirmationExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmationSentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnsubscribedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UnsubscribeKey = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsletterSubscribers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_ConfirmationTokenHash",
                table: "NewsletterSubscribers",
                column: "ConfirmationTokenHash",
                unique: true,
                filter: "[ConfirmationTokenHash] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_Email",
                table: "NewsletterSubscribers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_Status",
                table: "NewsletterSubscribers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NewsletterSubscribers_UnsubscribeKey",
                table: "NewsletterSubscribers",
                column: "UnsubscribeKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NewsletterSubscribers");
        }
    }
}
