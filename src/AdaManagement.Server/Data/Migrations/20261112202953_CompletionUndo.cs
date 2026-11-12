using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdaManagement.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompletionUndo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PreviousAnchorDate",
                table: "CompletionLog",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousOccurrenceIndex",
                table: "CompletionLog",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PreviousAnchorDate",
                table: "CompletionLog");

            migrationBuilder.DropColumn(
                name: "PreviousOccurrenceIndex",
                table: "CompletionLog");
        }
    }
}
