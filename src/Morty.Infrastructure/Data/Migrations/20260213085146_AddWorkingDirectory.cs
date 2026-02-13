using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morty.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkingDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WorkingDirectory",
                table: "Projects",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WorkingDirectory",
                table: "Projects");
        }
    }
}
