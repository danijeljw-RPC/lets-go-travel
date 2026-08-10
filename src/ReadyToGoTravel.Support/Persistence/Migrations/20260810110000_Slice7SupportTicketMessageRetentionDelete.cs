using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyToGoTravel.Support.Persistence.Migrations;

/// <inheritdoc />
public partial class Slice7SupportTicketMessageRetentionDelete : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Slice 6's reject_support_ticket_message_mutation trigger blocked both UPDATE and DELETE,
        // which was correct for its original purpose (no silent edit or piecemeal removal of a
        // live ticket's message history) but was never reconciled with Slice 7's approved
        // retention schedule, which requires deleting an entire ticket's message history once the
        // ticket itself reaches its 2-year/7-year expiry. A live PostgreSQL drill against this
        // trigger caught the conflict: SupportRetentionSweepProcessor.DeleteTicketAggregateAsync's
        // bulk delete of support_ticket_messages was rejected by this trigger on every cycle,
        // silently and permanently blocking every general/booking-related ticket deletion in
        // production - invisible to the SQLite-backed unit test suite because migration-only raw
        // trigger SQL is never executed against SQLite's EnsureCreatedAsync model. The trigger now
        // rejects only UPDATE; DELETE remains blocked nowhere else in the codebase except this one
        // legal-hold-aware, receipted retention path (confirmed by a full-repository search), so
        // relaxing it here does not reopen any other route to silently removing message history.
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS reject_support_ticket_message_mutation ON support.support_ticket_messages;

            CREATE OR REPLACE FUNCTION support.reject_support_ticket_message_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'support ticket messages are append-only';
            END;
            $function$;

            CREATE TRIGGER reject_support_ticket_message_mutation
            BEFORE UPDATE ON support.support_ticket_messages
            FOR EACH ROW
            EXECUTE FUNCTION support.reject_support_ticket_message_mutation();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS reject_support_ticket_message_mutation ON support.support_ticket_messages;

            CREATE OR REPLACE FUNCTION support.reject_support_ticket_message_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'support ticket messages are append-only';
            END;
            $function$;

            CREATE TRIGGER reject_support_ticket_message_mutation
            BEFORE UPDATE OR DELETE ON support.support_ticket_messages
            FOR EACH ROW
            EXECUTE FUNCTION support.reject_support_ticket_message_mutation();
            """);
    }
}
