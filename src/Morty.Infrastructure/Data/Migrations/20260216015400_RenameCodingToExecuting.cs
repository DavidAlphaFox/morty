using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morty.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameCodingToExecuting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 更新 Stories 表中的 Phase 字段
            migrationBuilder.Sql("UPDATE \"Stories\" SET \"Phase\" = 'Executing' WHERE \"Phase\" = 'Coding'");

            // 更新 PhaseHistories 表中的 Phase 字段
            migrationBuilder.Sql("UPDATE \"PhaseHistories\" SET \"Phase\" = 'Executing' WHERE \"Phase\" = 'Coding'");

            // 更新 EnvConfigRules 表中的阶段字段
            migrationBuilder.Sql("UPDATE \"EnvConfigRules\" SET \"FromPhase\" = 'Executing' WHERE \"FromPhase\" = 'Coding'");
            migrationBuilder.Sql("UPDATE \"EnvConfigRules\" SET \"ToPhase\" = 'Executing' WHERE \"ToPhase\" = 'Coding'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Stories\" SET \"Phase\" = 'Coding' WHERE \"Phase\" = 'Executing'");
            migrationBuilder.Sql("UPDATE \"PhaseHistories\" SET \"Phase\" = 'Coding' WHERE \"Phase\" = 'Executing'");
            migrationBuilder.Sql("UPDATE \"EnvConfigRules\" SET \"FromPhase\" = 'Coding' WHERE \"FromPhase\" = 'Executing'");
            migrationBuilder.Sql("UPDATE \"EnvConfigRules\" SET \"ToPhase\" = 'Coding' WHERE \"ToPhase\" = 'Executing'");
        }
    }
}
