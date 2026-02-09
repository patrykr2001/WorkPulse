using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkPlanner.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSprintsAndTaskOrdering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacklogItems");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "TaskItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SprintId",
                table: "TaskItems",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Sprints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sprints_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_SprintId",
                table: "TaskItems",
                column: "SprintId");

            migrationBuilder.CreateIndex(
                name: "IX_Sprints_ProjectId",
                table: "Sprints",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_Sprints_SprintId",
                table: "TaskItems",
                column: "SprintId",
                principalTable: "Sprints",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_Sprints_SprintId",
                table: "TaskItems");

            migrationBuilder.DropTable(
                name: "Sprints");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_SprintId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "SprintId",
                table: "TaskItems");

            migrationBuilder.CreateTable(
                name: "BacklogItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ProjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    TaskItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacklogItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacklogItems_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BacklogItems_TaskItems_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "TaskItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacklogItems_ProjectId",
                table: "BacklogItems",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_BacklogItems_TaskItemId",
                table: "BacklogItems",
                column: "TaskItemId");
        }
    }
}
