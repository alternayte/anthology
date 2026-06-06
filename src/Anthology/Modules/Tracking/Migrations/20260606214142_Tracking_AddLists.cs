using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthology.Modules.Tracking.Migrations
{
    /// <inheritdoc />
    public partial class Tracking_AddLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "list_items",
                schema: "tracking",
                columns: table => new
                {
                    list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<double>(type: "double precision", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    media_type = table.Column<string>(type: "text", nullable: false),
                    poster_path = table.Column<string>(type: "text", nullable: true),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_list_items", x => new { x.list_id, x.title_id });
                });

            migrationBuilder.CreateTable(
                name: "lists",
                schema: "tracking",
                columns: table => new
                {
                    list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    visibility = table.Column<string>(type: "text", nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lists", x => x.list_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_lists_user_id_created_at",
                schema: "tracking",
                table: "lists",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "list_items",
                schema: "tracking");

            migrationBuilder.DropTable(
                name: "lists",
                schema: "tracking");
        }
    }
}
