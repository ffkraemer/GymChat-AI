using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GymChatAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Update_20260721172734 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationTimeSlots_NotificationPreferences_NotificationPreferenceId",
                table: "NotificationTimeSlots");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationTimeSlots_NotificationPreferences_NotificationPreferenceId",
                table: "NotificationTimeSlots",
                column: "NotificationPreferenceId",
                principalTable: "NotificationPreferences",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NotificationTimeSlots_NotificationPreferences_NotificationPreferenceId",
                table: "NotificationTimeSlots");

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationTimeSlots_NotificationPreferences_NotificationPreferenceId",
                table: "NotificationTimeSlots",
                column: "NotificationPreferenceId",
                principalTable: "NotificationPreferences",
                principalColumn: "Id");
        }
    }
}
