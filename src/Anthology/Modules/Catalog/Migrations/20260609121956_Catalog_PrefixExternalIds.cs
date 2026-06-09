using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthology.Modules.Catalog.Migrations
{
    /// <inheritdoc />
    public partial class Catalog_PrefixExternalIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE catalog.titles SET external_id = 'tmdb-' || external_id WHERE external_id NOT LIKE 'tmdb-%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE catalog.titles SET external_id = REPLACE(external_id, 'tmdb-', '') WHERE external_id LIKE 'tmdb-%'");
        }
    }
}
