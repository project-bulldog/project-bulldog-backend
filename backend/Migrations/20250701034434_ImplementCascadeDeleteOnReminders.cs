using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class ImplementCascadeDeleteOnReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reminders_ActionItems_ActionItemId",
                table: "Reminders");

            migrationBuilder.AddForeignKey(
                name: "FK_Reminders_ActionItems_ActionItemId",
                table: "Reminders",
                column: "ActionItemId",
                principalTable: "ActionItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reminders_ActionItems_ActionItemId",
                table: "Reminders");

            migrationBuilder.AddForeignKey(
                name: "FK_Reminders_ActionItems_ActionItemId",
                table: "Reminders",
                column: "ActionItemId",
                principalTable: "ActionItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
