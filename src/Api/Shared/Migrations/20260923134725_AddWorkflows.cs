using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Shared.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiWorkflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorkflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiWorkflowEdges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FromKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ToKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorkflowEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiWorkflowEdges_AiWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "AiWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiWorkflowNodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Instruction = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    X = table.Column<double>(type: "double precision", nullable: false),
                    Y = table.Column<double>(type: "double precision", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorkflowNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiWorkflowNodes_AiAgents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "AiAgents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AiWorkflowNodes_AiWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "AiWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AiWorkflowRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Input = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GraphJson = table.Column<string>(type: "text", nullable: false),
                    PrincipalJson = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorkflowRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiWorkflowRuns_AiWorkflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "AiWorkflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiWorkflowRunSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NodeKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Output = table.Column<string>(type: "text", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: true),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    IterationsUsed = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiWorkflowRunSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiWorkflowRunSteps_AiWorkflowRuns_WorkflowRunId",
                        column: x => x.WorkflowRunId,
                        principalTable: "AiWorkflowRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkflowEdges_WorkflowId_FromKey_ToKey",
                table: "AiWorkflowEdges",
                columns: new[] { "WorkflowId", "FromKey", "ToKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkflowNodes_AgentId",
                table: "AiWorkflowNodes",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkflowNodes_WorkflowId_Key",
                table: "AiWorkflowNodes",
                columns: new[] { "WorkflowId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkflowRuns_Status",
                table: "AiWorkflowRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkflowRuns_TenantId_WorkflowId_CreatedAt",
                table: "AiWorkflowRuns",
                columns: new[] { "TenantId", "WorkflowId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkflowRuns_WorkflowId",
                table: "AiWorkflowRuns",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkflowRunSteps_WorkflowRunId_NodeKey",
                table: "AiWorkflowRunSteps",
                columns: new[] { "WorkflowRunId", "NodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiWorkflows_TenantId_UpdatedAt",
                table: "AiWorkflows",
                columns: new[] { "TenantId", "UpdatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiWorkflowEdges");

            migrationBuilder.DropTable(
                name: "AiWorkflowNodes");

            migrationBuilder.DropTable(
                name: "AiWorkflowRunSteps");

            migrationBuilder.DropTable(
                name: "AiWorkflowRuns");

            migrationBuilder.DropTable(
                name: "AiWorkflows");
        }
    }
}
