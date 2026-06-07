using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Anthology.Kernel.EventStore.Migrations
{
    /// <inheritdoc />
    public partial class EventStore_AddNotifyTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION es.notify_new_events() RETURNS trigger AS $$
                BEGIN
                    PERFORM pg_notify('new_events', '');
                    RETURN NULL;
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_events_notify
                    AFTER INSERT ON es.events
                    FOR EACH STATEMENT
                    EXECUTE FUNCTION es.notify_new_events();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_events_notify ON es.events;
                DROP FUNCTION IF EXISTS es.notify_new_events();
                """);
        }
    }
}
