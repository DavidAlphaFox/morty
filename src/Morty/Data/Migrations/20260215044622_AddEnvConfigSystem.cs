using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morty.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnvConfigSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaudeEnvConfigs");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Stories",
                type: "TEXT",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DefaultEnvConfigGroupId",
                table: "Projects",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EnvConfigGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvConfigGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnvConfigRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    EnvConfigGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromPhase = table.Column<string>(type: "TEXT", nullable: true),
                    ToPhase = table.Column<string>(type: "TEXT", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvConfigRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvConfigRules_EnvConfigGroups_EnvConfigGroupId",
                        column: x => x.EnvConfigGroupId,
                        principalTable: "EnvConfigGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnvConfigRules_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnvVariables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EnvConfigGroupId = table.Column<int>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultValue = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvVariables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvVariables_EnvConfigGroups_EnvConfigGroupId",
                        column: x => x.EnvConfigGroupId,
                        principalTable: "EnvConfigGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DefaultEnvConfigGroupId",
                table: "Projects",
                column: "DefaultEnvConfigGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvConfigRules_EnvConfigGroupId",
                table: "EnvConfigRules",
                column: "EnvConfigGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvConfigRules_ProjectId",
                table: "EnvConfigRules",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvVariables_EnvConfigGroupId",
                table: "EnvVariables",
                column: "EnvConfigGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_EnvConfigGroups_DefaultEnvConfigGroupId",
                table: "Projects",
                column: "DefaultEnvConfigGroupId",
                principalTable: "EnvConfigGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_EnvConfigGroups_DefaultEnvConfigGroupId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "EnvConfigRules");

            migrationBuilder.DropTable(
                name: "EnvVariables");

            migrationBuilder.DropTable(
                name: "EnvConfigGroups");

            migrationBuilder.DropIndex(
                name: "IX_Projects_DefaultEnvConfigGroupId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Stories");

            migrationBuilder.DropColumn(
                name: "DefaultEnvConfigGroupId",
                table: "Projects");

            migrationBuilder.CreateTable(
                name: "ClaudeEnvConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    DefaultValue = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaudeEnvConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaudeEnvConfigs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClaudeEnvConfigs_ProjectId",
                table: "ClaudeEnvConfigs",
                column: "ProjectId");
        }
    }
}
