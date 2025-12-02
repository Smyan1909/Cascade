using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cascade.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    target_application = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    active_version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "1.0.0"),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    capabilities = table.Column<string>(type: "TEXT", nullable: false),
                    instruction_list = table.Column<string>(type: "TEXT", nullable: false),
                    metadata = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_executed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "configurations",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: true),
                    type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "String"),
                    is_encrypted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_configurations", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "agent_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    agent_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    notes = table.Column<string>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    instruction_list_snapshot = table.Column<string>(type: "TEXT", nullable: false),
                    capabilities_snapshot = table.Column<string>(type: "TEXT", nullable: false),
                    script_ids_snapshot = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_versions_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "execution_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    agent_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    session_id = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    task_description = table.Column<string>(type: "TEXT", nullable: false),
                    success = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    error_message = table.Column<string>(type: "TEXT", nullable: true),
                    summary = table.Column<string>(type: "TEXT", nullable: true),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    duration_ms = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    result_data = table.Column<string>(type: "TEXT", nullable: true),
                    logs = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_execution_records_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exploration_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    target_application = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    instruction_manual = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    progress = table.Column<float>(type: "REAL", nullable: false, defaultValue: 0f),
                    goals = table.Column<string>(type: "TEXT", nullable: false),
                    completed_goals = table.Column<string>(type: "TEXT", nullable: false),
                    failed_goals = table.Column<string>(type: "TEXT", nullable: false),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    generated_agent_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exploration_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_exploration_sessions_agents_generated_agent_id",
                        column: x => x.generated_agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "scripts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    source_code = table.Column<string>(type: "TEXT", nullable: false),
                    current_version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "1.0.0"),
                    type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    compiled_assembly = table.Column<byte[]>(type: "BLOB", nullable: true),
                    last_compiled_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    compilation_errors = table.Column<string>(type: "TEXT", nullable: true),
                    metadata = table.Column<string>(type: "TEXT", nullable: false),
                    type_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    method_name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    agent_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scripts", x => x.id);
                    table.ForeignKey(
                        name: "FK_scripts_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "execution_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    execution_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    step_order = table.Column<int>(type: "INTEGER", nullable: false),
                    action = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    parameters = table.Column<string>(type: "TEXT", nullable: true),
                    success = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    error = table.Column<string>(type: "TEXT", nullable: true),
                    result = table.Column<string>(type: "TEXT", nullable: true),
                    duration_ms = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    screenshot = table.Column<byte[]>(type: "BLOB", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_execution_steps_execution_records_execution_id",
                        column: x => x.execution_id,
                        principalTable: "execution_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exploration_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    session_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    window_title = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    element_data = table.Column<string>(type: "TEXT", nullable: true),
                    action_test_result = table.Column<string>(type: "TEXT", nullable: true),
                    navigation_path = table.Column<string>(type: "TEXT", nullable: true),
                    screenshot = table.Column<byte[]>(type: "BLOB", nullable: true),
                    ocr_text = table.Column<string>(type: "TEXT", nullable: true),
                    captured_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exploration_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_exploration_results_exploration_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "exploration_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "script_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    script_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    source_code = table.Column<string>(type: "TEXT", nullable: false),
                    change_description = table.Column<string>(type: "TEXT", nullable: true),
                    compiled_assembly = table.Column<byte[]>(type: "BLOB", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_script_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_script_versions_scripts_script_id",
                        column: x => x.script_id,
                        principalTable: "scripts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_versions_agent_id",
                table: "agent_versions",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_versions_agent_id_version",
                table: "agent_versions",
                columns: new[] { "agent_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_name",
                table: "agents",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_status",
                table: "agents",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_agents_target_application",
                table: "agents",
                column: "target_application");

            migrationBuilder.CreateIndex(
                name: "IX_execution_records_agent_id",
                table: "execution_records",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_execution_records_started_at",
                table: "execution_records",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "IX_execution_records_user_id",
                table: "execution_records",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_execution_steps_execution_id",
                table: "execution_steps",
                column: "execution_id");

            migrationBuilder.CreateIndex(
                name: "IX_exploration_results_session_id",
                table: "exploration_results",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_exploration_results_type",
                table: "exploration_results",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "IX_exploration_sessions_generated_agent_id",
                table: "exploration_sessions",
                column: "generated_agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_exploration_sessions_status",
                table: "exploration_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_exploration_sessions_target_application",
                table: "exploration_sessions",
                column: "target_application");

            migrationBuilder.CreateIndex(
                name: "IX_script_versions_script_id",
                table: "script_versions",
                column: "script_id");

            migrationBuilder.CreateIndex(
                name: "IX_script_versions_script_id_version",
                table: "script_versions",
                columns: new[] { "script_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scripts_agent_id",
                table: "scripts",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_scripts_name",
                table: "scripts",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_scripts_type",
                table: "scripts",
                column: "type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_versions");

            migrationBuilder.DropTable(
                name: "configurations");

            migrationBuilder.DropTable(
                name: "execution_steps");

            migrationBuilder.DropTable(
                name: "exploration_results");

            migrationBuilder.DropTable(
                name: "script_versions");

            migrationBuilder.DropTable(
                name: "execution_records");

            migrationBuilder.DropTable(
                name: "exploration_sessions");

            migrationBuilder.DropTable(
                name: "scripts");

            migrationBuilder.DropTable(
                name: "agents");
        }
    }
}
