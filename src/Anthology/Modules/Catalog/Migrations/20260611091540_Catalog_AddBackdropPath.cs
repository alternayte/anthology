using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthology.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_AddBackdropPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "backdrop_path",
                schema: "catalog",
                table: "titles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "backdrop_path",
                schema: "catalog",
                table: "titles");
        }
    }
}
