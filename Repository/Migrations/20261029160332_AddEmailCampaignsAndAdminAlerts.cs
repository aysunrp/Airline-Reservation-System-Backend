using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailCampaignsAndAdminAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAlerts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAlerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PromoCodeId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DiscountPercent = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ValidTo = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MaxUses = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailCampaigns_PromoCodes_PromoCodeId",
                        column: x => x.PromoCodeId,
                        principalTable: "PromoCodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EmailCampaignRecipients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    SubscriberId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailCampaignRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailCampaignRecipients_EmailCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "EmailCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlerts_EventKey",
                table: "AdminAlerts",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminAlerts_SentAt_Attempts",
                table: "AdminAlerts",
                columns: new[] { "SentAt", "Attempts" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_CampaignId_Status",
                table: "EmailCampaignRecipients",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaignRecipients_CampaignId_SubscriberId",
                table: "EmailCampaignRecipients",
                columns: new[] { "CampaignId", "SubscriberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_PromoCodeId",
                table: "EmailCampaigns",
                column: "PromoCodeId",
                unique: true,
                filter: "[PromoCodeId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailCampaigns_Status",
                table: "EmailCampaigns",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAlerts");

            migrationBuilder.DropTable(
                name: "EmailCampaignRecipients");

            migrationBuilder.DropTable(
                name: "EmailCampaigns");
        }
    }
}
