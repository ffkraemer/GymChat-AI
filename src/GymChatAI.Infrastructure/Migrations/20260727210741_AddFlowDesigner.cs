using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymChatAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFlowDesigner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FlowScreens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WhatsAppFlowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScreenId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowScreens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowScreens_WhatsAppFlows_WhatsAppFlowId",
                        column: x => x.WhatsAppFlowId,
                        principalTable: "WhatsAppFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlowComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlowScreenId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    VariableName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Required = table.Column<bool>(type: "bit", nullable: false),
                    OptionsSource = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    StaticOptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FooterAction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FooterNextScreenId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FooterButtonLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowComponents_FlowScreens_FlowScreenId",
                        column: x => x.FlowScreenId,
                        principalTable: "FlowScreens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlowComponents_FlowScreenId",
                table: "FlowComponents",
                column: "FlowScreenId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowScreens_WhatsAppFlowId",
                table: "FlowScreens",
                column: "WhatsAppFlowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FlowComponents");

            migrationBuilder.DropTable(
                name: "FlowScreens");
        }
    }
}
