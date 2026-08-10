using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyToGoTravel.Retention.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialRetentionSchema : Migration
{
    private static readonly string[] RecordClassComponentBookingIdColumns = ["record_class", "component_booking_id"];
    private static readonly string[] RecordClassCustomerIdColumns = ["record_class", "customer_id"];
    private static readonly string[] RecordClassSupportTicketIdColumns = ["record_class", "support_ticket_id"];
    private static readonly string[] RecordClassCompletedAtUtcColumns = ["record_class", "completed_at_utc"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "retention");

        migrationBuilder.CreateTable(
            name: "legal_holds",
            schema: "retention",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                matter_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                authorized_owner_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                review_by_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                released_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                released_by_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                release_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_legal_holds", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "retention_deletion_receipts",
            schema: "retention",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                record_class = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                policy_version = table.Column<int>(type: "integer", nullable: false),
                action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                success_count = table.Column<int>(type: "integer", nullable: false),
                failure_count = table.Column<int>(type: "integer", nullable: false),
                completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                failure_summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_retention_deletion_receipts", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "retention_operational_cases",
            schema: "retention",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                record_class = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                scope = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                dedupe_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_retention_operational_cases", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "legal_hold_audit_events",
            schema: "retention",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                legal_hold_id = table.Column<Guid>(type: "uuid", nullable: false),
                event_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                actor_subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_legal_hold_audit_events", x => x.id);
                table.ForeignKey(
                    name: "FK_legal_hold_audit_events_legal_holds_legal_hold_id",
                    column: x => x.legal_hold_id,
                    principalSchema: "retention",
                    principalTable: "legal_holds",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "legal_hold_scopes",
            schema: "retention",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                legal_hold_id = table.Column<Guid>(type: "uuid", nullable: false),
                record_class = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                component_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                support_ticket_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_legal_hold_scopes", x => x.id);
                table.CheckConstraint("ck_legal_hold_scopes_exactly_one_subject", "(CASE WHEN customer_id IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN component_booking_id IS NOT NULL THEN 1 ELSE 0 END) + (CASE WHEN support_ticket_id IS NOT NULL THEN 1 ELSE 0 END) = 1");
                table.ForeignKey(
                    name: "FK_legal_hold_scopes_legal_holds_legal_hold_id",
                    column: x => x.legal_hold_id,
                    principalSchema: "retention",
                    principalTable: "legal_holds",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_legal_hold_audit_events_legal_hold_id",
            schema: "retention",
            table: "legal_hold_audit_events",
            column: "legal_hold_id");

        migrationBuilder.CreateIndex(
            name: "IX_legal_hold_scopes_legal_hold_id",
            schema: "retention",
            table: "legal_hold_scopes",
            column: "legal_hold_id");

        migrationBuilder.CreateIndex(
            name: "IX_legal_hold_scopes_record_class_component_booking_id",
            schema: "retention",
            table: "legal_hold_scopes",
            columns: RecordClassComponentBookingIdColumns);

        migrationBuilder.CreateIndex(
            name: "IX_legal_hold_scopes_record_class_customer_id",
            schema: "retention",
            table: "legal_hold_scopes",
            columns: RecordClassCustomerIdColumns);

        migrationBuilder.CreateIndex(
            name: "IX_legal_hold_scopes_record_class_support_ticket_id",
            schema: "retention",
            table: "legal_hold_scopes",
            columns: RecordClassSupportTicketIdColumns);

        migrationBuilder.CreateIndex(
            name: "IX_legal_holds_released_at_utc",
            schema: "retention",
            table: "legal_holds",
            column: "released_at_utc");

        migrationBuilder.CreateIndex(
            name: "IX_retention_deletion_receipts_record_class_completed_at_utc",
            schema: "retention",
            table: "retention_deletion_receipts",
            columns: RecordClassCompletedAtUtcColumns);

        migrationBuilder.CreateIndex(
            name: "IX_retention_operational_cases_dedupe_key",
            schema: "retention",
            table: "retention_operational_cases",
            column: "dedupe_key",
            unique: true);

        // Defence in depth alongside RetentionDbContext.SaveChanges's change-tracker guard,
        // mirroring booking.reject_booking_version_mutation() and treating legal-hold history
        // and deletion receipts with the same tamper-evidence rigor as canonical booking
        // version history: reject any UPDATE/DELETE outside EF's own guarded save path too.
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION retention.reject_legal_hold_audit_event_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'legal hold audit events are append-only';
            END;
            $function$;

            CREATE TRIGGER reject_legal_hold_audit_event_mutation
            BEFORE UPDATE OR DELETE ON retention.legal_hold_audit_events
            FOR EACH ROW
            EXECUTE FUNCTION retention.reject_legal_hold_audit_event_mutation();
            """);

        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION retention.reject_retention_deletion_receipt_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'retention deletion receipts are append-only';
            END;
            $function$;

            CREATE TRIGGER reject_retention_deletion_receipt_mutation
            BEFORE UPDATE OR DELETE ON retention.retention_deletion_receipts
            FOR EACH ROW
            EXECUTE FUNCTION retention.reject_retention_deletion_receipt_mutation();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS reject_retention_deletion_receipt_mutation ON retention.retention_deletion_receipts;
            DROP FUNCTION IF EXISTS retention.reject_retention_deletion_receipt_mutation();
            DROP TRIGGER IF EXISTS reject_legal_hold_audit_event_mutation ON retention.legal_hold_audit_events;
            DROP FUNCTION IF EXISTS retention.reject_legal_hold_audit_event_mutation();
            """);

        migrationBuilder.DropTable(
            name: "legal_hold_audit_events",
            schema: "retention");

        migrationBuilder.DropTable(
            name: "legal_hold_scopes",
            schema: "retention");

        migrationBuilder.DropTable(
            name: "retention_deletion_receipts",
            schema: "retention");

        migrationBuilder.DropTable(
            name: "retention_operational_cases",
            schema: "retention");

        migrationBuilder.DropTable(
            name: "legal_holds",
            schema: "retention");
    }
}
