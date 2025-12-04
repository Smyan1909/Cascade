using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cascade.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "automation_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    session_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    agent_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    execution_record_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    run_id = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    profile = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, defaultValue: "Active"),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    released_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_automation_sessions_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_automation_sessions_execution_records_execution_record_id",
                        column: x => x.execution_record_id,
                        principalTable: "execution_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "session_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    automation_session_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    event_type = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    payload = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}"),
                    occurred_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_events_automation_sessions_automation_session_id",
                        column: x => x.automation_session_id,
                        principalTable: "automation_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_automation_sessions_agent_id",
                table: "automation_sessions",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_automation_sessions_execution_record_id",
                table: "automation_sessions",
                column: "execution_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_automation_sessions_session_id",
                table: "automation_sessions",
                column: "session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_session_events_session",
                table: "session_events",
                column: "automation_session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_events");

            migrationBuilder.DropTable(
                name: "automation_sessions");
        }
    }
}
