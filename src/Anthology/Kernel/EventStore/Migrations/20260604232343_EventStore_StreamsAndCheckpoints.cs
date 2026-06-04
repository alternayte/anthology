using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthology.Kernel.EventStore.Migrations
{
    /// <inheritdoc />
    public partial class EventStore_StreamsAndCheckpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<ulong>(
                name: "Xid",
                schema: "es",
                table: "events",
                type: "xid8",
                nullable: false,
                defaultValueSql: "pg_current_xact_id()");

            migrationBuilder.CreateTable(
                name: "checkpoints",
                schema: "es",
                columns: table => new
                {
                    ProjectionName = table.Column<string>(type: "text", nullable: false),
                    Position = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkpoints", x => x.ProjectionName);
                });

            migrationBuilder.CreateTable(
                name: "streams",
                schema: "es",
                columns: table => new
                {
                    StreamId = table.Column<Guid>(type: "uuid", nullable: false),
                    StreamType = table.Column<string>(type: "text", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streams", x => x.StreamId);
                });

            migrationBuilder.Sql("""
                INSERT INTO es.streams (stream_id, stream_type, version, state, created_at, updated_at)
                SELECT stream_id, 'tracked_item',
                       MAX(version), '{}'::jsonb, MIN(occurred_at), MAX(occurred_at)
                FROM es.events
                GROUP BY stream_id
                ON CONFLICT DO NOTHING
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_events_streams_StreamId",
                schema: "es",
                table: "events",
                column: "StreamId",
                principalSchema: "es",
                principalTable: "streams",
                principalColumn: "StreamId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_events_streams_StreamId",
                schema: "es",
                table: "events");

            migrationBuilder.DropTable(
                name: "checkpoints",
                schema: "es");

            migrationBuilder.DropTable(
                name: "streams",
                schema: "es");

            migrationBuilder.DropColumn(
                name: "Xid",
                schema: "es",
                table: "events");
        }
    }
}
