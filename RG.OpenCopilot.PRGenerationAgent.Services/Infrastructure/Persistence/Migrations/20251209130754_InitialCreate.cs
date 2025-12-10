using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RG.OpenCopilot.PRGenerationAgent.Services.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_tasks",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    installation_id = table.Column<long>(type: "bigint", nullable: false),
                    repository_owner = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    repository_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    issue_number = table.Column<int>(type: "integer", nullable: false),
                    plan = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_tasks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_created_at",
                table: "agent_tasks",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_installation_id",
                table: "agent_tasks",
                column: "installation_id");

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_repository",
                table: "agent_tasks",
                columns: new[] { "repository_owner", "repository_name" });

            migrationBuilder.CreateIndex(
                name: "ix_agent_tasks_status",
                table: "agent_tasks",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_tasks");
        }
    }
}
