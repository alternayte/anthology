using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthology.Modules.Tracking.Migrations
{
    /// <inheritdoc />
    public partial class Tracking_AddPosterPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "poster_path",
                schema: "tracking",
                table: "library_items",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "poster_path",
                schema: "tracking",
                table: "library_items");
        }
    }
}
