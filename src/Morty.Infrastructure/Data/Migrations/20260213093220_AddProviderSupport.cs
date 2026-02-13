using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morty.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Output",
                table: "Plans",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "Plans",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Plans",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProviderId",
                table: "Iterations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Providers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ApiUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Model = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Token = table.Column<string>(type: "TEXT", nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionOutputs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IterationId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderId = table.Column<int>(type: "INTEGER", nullable: true),
                    Prompt = table.Column<string>(type: "TEXT", nullable: false),
                    Response = table.Column<string>(type: "TEXT", nullable: false),
                    ParsedOutput = table.Column<string>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<int>(type: "INTEGER", nullable: true),
                    CostUsd = table.Column<decimal>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionOutputs_Iterations_IterationId",
                        column: x => x.IterationId,
                        principalTable: "Iterations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutionOutputs_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Plans_ProviderId",
                table: "Plans",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Iterations_ProviderId",
                table: "Iterations",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionOutputs_IterationId",
                table: "ExecutionOutputs",
                column: "IterationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionOutputs_ProviderId",
                table: "ExecutionOutputs",
                column: "ProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Iterations_Providers_ProviderId",
                table: "Iterations",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Plans_Providers_ProviderId",
                table: "Plans",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Iterations_Providers_ProviderId",
                table: "Iterations");

            migrationBuilder.DropForeignKey(
                name: "FK_Plans_Providers_ProviderId",
                table: "Plans");

            migrationBuilder.DropTable(
                name: "ExecutionOutputs");

            migrationBuilder.DropTable(
                name: "Providers");

            migrationBuilder.DropIndex(
                name: "IX_Plans_ProviderId",
                table: "Plans");

            migrationBuilder.DropIndex(
                name: "IX_Iterations_ProviderId",
                table: "Iterations");

            migrationBuilder.DropColumn(
                name: "Output",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "ProviderId",
                table: "Iterations");
        }
    }
}
