using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymChatAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkCampaignsToWhatsAppTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActualCategory",
                table: "WhatsAppMessageTemplates",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppBusinessAccountId",
                table: "WhatsAppMessageTemplates",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppBusinessAccountId",
                table: "WhatsAppFlows",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WhatsAppMessageTemplateId",
                table: "Campaigns",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualCategory",
                table: "WhatsAppMessageTemplates");

            migrationBuilder.DropColumn(
                name: "WhatsAppBusinessAccountId",
                table: "WhatsAppMessageTemplates");

            migrationBuilder.DropColumn(
                name: "WhatsAppBusinessAccountId",
                table: "WhatsAppFlows");

            migrationBuilder.DropColumn(
                name: "WhatsAppMessageTemplateId",
                table: "Campaigns");
        }
    }
}
