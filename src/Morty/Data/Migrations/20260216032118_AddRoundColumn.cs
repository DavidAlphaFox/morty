using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morty.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "StoryEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "Stories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "PhaseHistories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "Iterations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Round",
                table: "StoryEvents");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "PhaseHistories");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "Iterations");
        }
    }
}
