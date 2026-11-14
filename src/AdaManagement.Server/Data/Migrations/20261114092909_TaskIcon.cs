using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdaManagement.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class TaskIcon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Icon",
                table: "Tasks",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Icon",
                table: "Tasks");
        }
    }
}
