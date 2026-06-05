using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthology.Modules.Tracking.Migrations
{
    /// <inheritdoc />
    public partial class Tracking_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tracking");

            migrationBuilder.CreateTable(
                name: "diary_entries",
                schema: "tracking",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    visibility = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_diary_entries", x => new { x.user_id, x.title_id, x.occurred_at });
                });

            migrationBuilder.CreateTable(
                name: "library_items",
                schema: "tracking",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    media_type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: true),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    visibility = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_library_items", x => new { x.user_id, x.title_id });
                });

            migrationBuilder.CreateIndex(
                name: "ix_diary_entries_user_id_occurred_at",
                schema: "tracking",
                table: "diary_entries",
                columns: new[] { "user_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_library_items_user_id_added_at_title_id",
                schema: "tracking",
                table: "library_items",
                columns: new[] { "user_id", "added_at", "title_id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_library_items_user_id_rating_title_id",
                schema: "tracking",
                table: "library_items",
                columns: new[] { "user_id", "rating", "title_id" },
                descending: new[] { false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "diary_entries",
                schema: "tracking");

            migrationBuilder.DropTable(
                name: "library_items",
                schema: "tracking");
        }
    }
}
