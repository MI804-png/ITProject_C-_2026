using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ITHelpdesk.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketPriorityAndDueDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_TicketStatusId",
                table: "Tickets");

            migrationBuilder.AlterColumn<string>(
                name: "AssignedToUserId",
                table: "Tickets",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueAtUtc",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AssignedToUserId",
                table: "Tickets",
                column: "AssignedToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketStatusId_Priority_CreatedAtUtc",
                table: "Tickets",
                columns: new[] { "TicketStatusId", "Priority", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_AssignedToUserId",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_TicketStatusId_Priority_CreatedAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "DueAtUtc",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Tickets");

            migrationBuilder.AlterColumn<string>(
                name: "AssignedToUserId",
                table: "Tickets",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TicketStatusId",
                table: "Tickets",
                column: "TicketStatusId");
        }
    }
}
