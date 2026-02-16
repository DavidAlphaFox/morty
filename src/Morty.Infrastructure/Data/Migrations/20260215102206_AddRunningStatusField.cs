using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morty.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRunningStatusField : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsPaused",
                table: "Stories",
                newName: "RunningStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "RunningStatus",
                table: "Stories",
                newName: "IsPaused");
        }
    }
}
