using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthology.Modules.Tracking.Migrations
{
    /// <inheritdoc />
    public partial class Tracking_AddTvProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "parts_completed",
                schema: "tracking",
                table: "library_items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "parts_total",
                schema: "tracking",
                table: "library_items",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "parts_completed",
                schema: "tracking",
                table: "library_items");

            migrationBuilder.DropColumn(
                name: "parts_total",
                schema: "tracking",
                table: "library_items");
        }
    }
}
