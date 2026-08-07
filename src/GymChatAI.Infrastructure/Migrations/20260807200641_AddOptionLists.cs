using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymChatAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OptionListId",
                table: "FlowComponents",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OptionLists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GymId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Key = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptionLists_Gyms_GymId",
                        column: x => x.GymId,
                        principalTable: "Gyms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OptionListItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionListId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionListItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OptionListItems_OptionLists_OptionListId",
                        column: x => x.OptionListId,
                        principalTable: "OptionLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FlowComponents_OptionListId",
                table: "FlowComponents",
                column: "OptionListId");

            migrationBuilder.CreateIndex(
                name: "IX_OptionListItems_OptionListId",
                table: "OptionListItems",
                column: "OptionListId");

            migrationBuilder.CreateIndex(
                name: "IX_OptionLists_GymId_Key",
                table: "OptionLists",
                columns: new[] { "GymId", "Key" },
                unique: true,
                filter: "[GymId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_FlowComponents_OptionLists_OptionListId",
                table: "FlowComponents",
                column: "OptionListId",
                principalTable: "OptionLists",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FlowComponents_OptionLists_OptionListId",
                table: "FlowComponents");

            migrationBuilder.DropTable(
                name: "OptionListItems");

            migrationBuilder.DropTable(
                name: "OptionLists");

            migrationBuilder.DropIndex(
                name: "IX_FlowComponents_OptionListId",
                table: "FlowComponents");

            migrationBuilder.DropColumn(
                name: "OptionListId",
                table: "FlowComponents");
        }
    }
}
