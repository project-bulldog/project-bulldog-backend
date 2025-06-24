using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleTimeParamtersToMultipleTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Users",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Summaries",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "Reminders",
                newName: "CreatedAtUtc");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtLocal",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtLocal",
                table: "Summaries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtLocal",
                table: "Reminders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtLocal",
                table: "ActionItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAtUtc",
                table: "ActionItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAtLocal",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedAtLocal",
                table: "Summaries");

            migrationBuilder.DropColumn(
                name: "CreatedAtLocal",
                table: "Reminders");

            migrationBuilder.DropColumn(
                name: "CreatedAtLocal",
                table: "ActionItems");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "ActionItems");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Users",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Summaries",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "Reminders",
                newName: "CreatedAt");
        }
    }
}
