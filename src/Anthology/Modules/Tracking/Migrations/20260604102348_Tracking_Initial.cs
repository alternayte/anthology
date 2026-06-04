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
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TitleId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    Visibility = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diary_entries", x => new { x.UserId, x.TitleId, x.OccurredAt });
                });

            migrationBuilder.CreateTable(
                name: "library_items",
                schema: "tracking",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TitleId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaType = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Visibility = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_library_items", x => new { x.UserId, x.TitleId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_diary_entries_UserId_OccurredAt",
                schema: "tracking",
                table: "diary_entries",
                columns: new[] { "UserId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_library_items_UserId_AddedAt_TitleId",
                schema: "tracking",
                table: "library_items",
                columns: new[] { "UserId", "AddedAt", "TitleId" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_library_items_UserId_Rating_TitleId",
                schema: "tracking",
                table: "library_items",
                columns: new[] { "UserId", "Rating", "TitleId" },
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
