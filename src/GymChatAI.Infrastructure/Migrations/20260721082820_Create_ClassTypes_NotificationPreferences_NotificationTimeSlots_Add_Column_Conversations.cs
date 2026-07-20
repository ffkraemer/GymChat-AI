using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymChatAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Create_ClassTypes_NotificationPreferences_NotificationTimeSlots_Add_Column_Conversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FlowStep",
                table: "Conversations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PendingFlowData",
                table: "Conversations",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClassTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GymId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassTypes_Gyms_GymId",
                        column: x => x.GymId,
                        principalTable: "Gyms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GymId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactPhoneNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsOnboarded = table.Column<bool>(type: "bit", nullable: false),
                    OptedIntoNotifications = table.Column<bool>(type: "bit", nullable: false),
                    SelectedClassTypeIds = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPreferences_Gyms_GymId",
                        column: x => x.GymId,
                        principalTable: "Gyms",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NotificationTimeSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NotificationPreferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DayOfWeek = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimeWindow = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTimeSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationTimeSlots_NotificationPreferences_NotificationPreferenceId",
                        column: x => x.NotificationPreferenceId,
                        principalTable: "NotificationPreferences",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassTypes_GymId_IsActive",
                table: "ClassTypes",
                columns: new[] { "GymId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPreferences_GymId_ContactPhoneNumber",
                table: "NotificationPreferences",
                columns: new[] { "GymId", "ContactPhoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTimeSlots_NotificationPreferenceId",
                table: "NotificationTimeSlots",
                column: "NotificationPreferenceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassTypes");

            migrationBuilder.DropTable(
                name: "NotificationTimeSlots");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropColumn(
                name: "FlowStep",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "PendingFlowData",
                table: "Conversations");
        }
    }
}
