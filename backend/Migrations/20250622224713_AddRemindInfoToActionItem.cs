using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddRemindInfoToActionItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Reminders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "ReminderMinutesBeforeDue",
                table: "ActionItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShouldRemind",
                table: "ActionItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "ReminderMinutesBeforeDue",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "ShouldRemind",
                table: "ActionItems");
        }
    }
}
