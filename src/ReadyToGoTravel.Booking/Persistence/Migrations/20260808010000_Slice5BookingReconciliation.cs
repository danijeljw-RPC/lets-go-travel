using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyToGoTravel.Booking.Persistence.Migrations;

/// <inheritdoc />
public partial class Slice5BookingReconciliation : Migration
{
    private static readonly string[] VersionHashColumns = ["component_booking_id", "canonical_hash"];
    private static readonly string[] VersionNumberColumns = ["component_booking_id", "version_number"];
    private static readonly string[] NotificationScheduleColumns = ["status", "not_before"];
    private static readonly string[] ReconciliationAttemptColumns = ["work_id", "attempt_number"];
    private static readonly string[] ReconciliationScheduleColumns = ["product", "status", "due_at"];
    private static readonly string[] WebhookIdentityColumns = ["provider", "environment", "event_id"];
    private static readonly string[] WebhookScheduleColumns = ["status", "next_attempt_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "current_canonical_hash",
            schema: "booking",
            table: "component_bookings",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "current_version_number",
            schema: "booking",
            table: "component_bookings",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "last_reconciled_at",
            schema: "booking",
            table: "component_bookings",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "next_departure_at",
            schema: "booking",
            table: "component_bookings",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "customer_locale",
            schema: "booking",
            table: "checkout_sessions",
            type: "character varying(35)",
            maxLength: 35,
            nullable: false,
            defaultValue: "en-AU");

        migrationBuilder.AddColumn<string>(
            name: "notification_time_zone_id",
            schema: "booking",
            table: "checkout_sessions",
            type: "character varying(80)",
            maxLength: 80,
            nullable: false,
            defaultValue: "Australia/Sydney");

        migrationBuilder.CreateTable(
            name: "booking_versions",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                component_booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                version_number = table.Column<int>(type: "integer", nullable: false),
                observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                effective_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                canonicalisation_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                canonical_snapshot_json = table.Column<string>(type: "text", nullable: false),
                canonical_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                metadata_json = table.Column<string>(type: "text", nullable: false),
                flags_json = table.Column<string>(type: "text", nullable: false),
                diff_json = table.Column<string>(type: "text", nullable: false),
                severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                next_departure_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_booking_versions", x => x.id);
                table.ForeignKey(
                    name: "FK_booking_versions_component_bookings_component_booking_id",
                    column: x => x.component_booking_id,
                    principalSchema: "booking",
                    principalTable: "component_bookings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "operational_cases",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                component_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                dedupe_key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                reason = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_operational_cases", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "reconciliation_work",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                component_booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                product = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                lease_owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                lease_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                attempts = table.Column<int>(type: "integer", nullable: false),
                source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                last_error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reconciliation_work", x => x.id);
                table.ForeignKey(
                    name: "FK_reconciliation_work_component_bookings_component_booking_id",
                    column: x => x.component_booking_id,
                    principalSchema: "booking",
                    principalTable: "component_bookings",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "webhook_inbox",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                event_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                event_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                raw_body = table.Column<string>(type: "text", nullable: false),
                payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                sandbox = table.Column<bool>(type: "boolean", nullable: false),
                correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                lease_owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                lease_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_webhook_inbox", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "notification_outbox",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                checkout_id = table.Column<Guid>(type: "uuid", nullable: false),
                component_booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                booking_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                dedupe_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                template = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                severity = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                locale = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                time_zone_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                payload_json = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false),
                not_before = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                lease_owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                lease_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                delivery_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_outbox", x => x.id);
                table.ForeignKey(
                    name: "FK_notification_outbox_booking_versions_booking_version_id",
                    column: x => x.booking_version_id,
                    principalSchema: "booking",
                    principalTable: "booking_versions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "reconciliation_attempts",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                work_id = table.Column<Guid>(type: "uuid", nullable: false),
                component_booking_id = table.Column<Guid>(type: "uuid", nullable: false),
                attempt_number = table.Column<int>(type: "integer", nullable: false),
                started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                correlation_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_reconciliation_attempts", x => x.id);
                table.ForeignKey(
                    name: "FK_reconciliation_attempts_reconciliation_work_work_id",
                    column: x => x.work_id,
                    principalSchema: "booking",
                    principalTable: "reconciliation_work",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_booking_versions_component_booking_id_canonical_hash",
            schema: "booking",
            table: "booking_versions",
            columns: VersionHashColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_booking_versions_component_booking_id_version_number",
            schema: "booking",
            table: "booking_versions",
            columns: VersionNumberColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_notification_outbox_booking_version_id",
            schema: "booking",
            table: "notification_outbox",
            column: "booking_version_id");

        migrationBuilder.CreateIndex(
            name: "IX_notification_outbox_dedupe_key",
            schema: "booking",
            table: "notification_outbox",
            column: "dedupe_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_notification_outbox_status_not_before",
            schema: "booking",
            table: "notification_outbox",
            columns: NotificationScheduleColumns);

        migrationBuilder.CreateIndex(
            name: "IX_operational_cases_dedupe_key",
            schema: "booking",
            table: "operational_cases",
            column: "dedupe_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_reconciliation_attempts_work_id_attempt_number",
            schema: "booking",
            table: "reconciliation_attempts",
            columns: ReconciliationAttemptColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_reconciliation_work_component_booking_id",
            schema: "booking",
            table: "reconciliation_work",
            column: "component_booking_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_reconciliation_work_product_status_due_at",
            schema: "booking",
            table: "reconciliation_work",
            columns: ReconciliationScheduleColumns);

        migrationBuilder.CreateIndex(
            name: "IX_webhook_inbox_provider_environment_event_id",
            schema: "booking",
            table: "webhook_inbox",
            columns: WebhookIdentityColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_webhook_inbox_status_next_attempt_at",
            schema: "booking",
            table: "webhook_inbox",
            columns: WebhookScheduleColumns);

        migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION booking.reject_booking_version_mutation()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $function$
                BEGIN
                    RAISE EXCEPTION 'canonical booking versions are append-only';
                END;
                $function$;

                CREATE TRIGGER reject_booking_version_mutation
                BEFORE UPDATE OR DELETE ON booking.booking_versions
                FOR EACH ROW
                EXECUTE FUNCTION booking.reject_booking_version_mutation();
                """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS reject_booking_version_mutation ON booking.booking_versions;
                DROP FUNCTION IF EXISTS booking.reject_booking_version_mutation();
                """);

        migrationBuilder.DropTable(
            name: "notification_outbox",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "operational_cases",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "reconciliation_attempts",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "webhook_inbox",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "booking_versions",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "reconciliation_work",
            schema: "booking");

        migrationBuilder.DropColumn(
            name: "current_canonical_hash",
            schema: "booking",
            table: "component_bookings");

        migrationBuilder.DropColumn(
            name: "current_version_number",
            schema: "booking",
            table: "component_bookings");

        migrationBuilder.DropColumn(
            name: "last_reconciled_at",
            schema: "booking",
            table: "component_bookings");

        migrationBuilder.DropColumn(
            name: "next_departure_at",
            schema: "booking",
            table: "component_bookings");

        migrationBuilder.DropColumn(
            name: "customer_locale",
            schema: "booking",
            table: "checkout_sessions");

        migrationBuilder.DropColumn(
            name: "notification_time_zone_id",
            schema: "booking",
            table: "checkout_sessions");
    }
}
