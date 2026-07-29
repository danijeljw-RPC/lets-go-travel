using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyToGoTravel.Booking.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialBookingSchema : Migration
{
    private static readonly string[] RecoveryCaseIdentityColumns = ["checkout_session_id", "dedupe_key", "reason"];
    private static readonly string[] CheckoutRevisionIdentityColumns = ["checkout_session_id", "number"];
    private static readonly string[] IdempotencyIdentityColumns = ["customer_id", "operation", "key"];
    private static readonly string[] TravellerSnapshotIdentityColumns = ["checkout_session_id", "traveller_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "booking");

        migrationBuilder.CreateTable(
            name: "checkout_sessions",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                trip_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_checkout_sessions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "idempotency_records",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                operation = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                response_status_code = table.Column<int>(type: "integer", nullable: true),
                response_body = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_idempotency_records", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "booking_recovery_cases",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                component_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                dedupe_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                reason = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                checkout_session_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_booking_recovery_cases", x => x.id);
                table.ForeignKey(
                    name: "FK_booking_recovery_cases_checkout_sessions_checkout_session_id",
                    column: x => x.checkout_session_id,
                    principalSchema: "booking",
                    principalTable: "checkout_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "checkout_acceptances",
            schema: "booking",
            columns: table => new
            {
                checkout_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                revision_number = table.Column<int>(type: "integer", nullable: false),
                accepted_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                terms_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                policy_version = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_checkout_acceptances", x => x.checkout_session_id);
                table.ForeignKey(
                    name: "FK_checkout_acceptances_checkout_sessions_checkout_session_id",
                    column: x => x.checkout_session_id,
                    principalSchema: "booking",
                    principalTable: "checkout_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "checkout_revisions",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                number = table.Column<int>(type: "integer", nullable: false),
                total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                transaction_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                terms_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                checkout_session_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_checkout_revisions", x => x.id);
                table.ForeignKey(
                    name: "FK_checkout_revisions_checkout_sessions_checkout_session_id",
                    column: x => x.checkout_session_id,
                    principalSchema: "booking",
                    principalTable: "checkout_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "component_bookings",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                product = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                offer_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                provider_binding = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                provider_booking_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                failure_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                checkout_session_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_component_bookings", x => x.id);
                table.ForeignKey(
                    name: "FK_component_bookings_checkout_sessions_checkout_session_id",
                    column: x => x.checkout_session_id,
                    principalSchema: "booking",
                    principalTable: "checkout_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "payment_attempts",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                provider_payment_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                provider_return_reference = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                failure_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                checkout_session_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_payment_attempts", x => x.id);
                table.ForeignKey(
                    name: "FK_payment_attempts_checkout_sessions_checkout_session_id",
                    column: x => x.checkout_session_id,
                    principalSchema: "booking",
                    principalTable: "checkout_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "traveller_snapshots",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                traveller_id = table.Column<Guid>(type: "uuid", nullable: false),
                given_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                family_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                is_minor = table.Column<bool>(type: "boolean", nullable: false),
                guardian_authority_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                age_at_travel = table.Column<int>(type: "integer", nullable: true),
                checkout_session_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_traveller_snapshots", x => x.id);
                table.ForeignKey(
                    name: "FK_traveller_snapshots_checkout_sessions_checkout_session_id",
                    column: x => x.checkout_session_id,
                    principalSchema: "booking",
                    principalTable: "checkout_sessions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "checkout_revision_components",
            schema: "booking",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                product = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                offer_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                provider_binding = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                product_detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                minimum_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                provider_revision = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                terms_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                checkout_revision_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_checkout_revision_components", x => x.id);
                table.ForeignKey(
                    name: "FK_checkout_revision_components_checkout_revisions_checkout_re~",
                    column: x => x.checkout_revision_id,
                    principalSchema: "booking",
                    principalTable: "checkout_revisions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ux_booking_recovery_cases_checkout_dedupe_reason",
            schema: "booking",
            table: "booking_recovery_cases",
            columns: RecoveryCaseIdentityColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_checkout_revision_components_checkout_revision_id",
            schema: "booking",
            table: "checkout_revision_components",
            column: "checkout_revision_id");

        migrationBuilder.CreateIndex(
            name: "IX_checkout_revisions_checkout_session_id_number",
            schema: "booking",
            table: "checkout_revisions",
            columns: CheckoutRevisionIdentityColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_checkout_sessions_customer_id",
            schema: "booking",
            table: "checkout_sessions",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "IX_checkout_sessions_trip_id",
            schema: "booking",
            table: "checkout_sessions",
            column: "trip_id");

        migrationBuilder.CreateIndex(
            name: "IX_component_bookings_checkout_session_id",
            schema: "booking",
            table: "component_bookings",
            column: "checkout_session_id");

        migrationBuilder.CreateIndex(
            name: "IX_component_bookings_provider_booking_reference",
            schema: "booking",
            table: "component_bookings",
            column: "provider_booking_reference",
            unique: true,
            filter: "provider_booking_reference IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "ux_idempotency_records_customer_operation_key",
            schema: "booking",
            table: "idempotency_records",
            columns: IdempotencyIdentityColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_payment_attempts_checkout_session_id",
            schema: "booking",
            table: "payment_attempts",
            column: "checkout_session_id");

        migrationBuilder.CreateIndex(
            name: "IX_payment_attempts_provider_return_reference",
            schema: "booking",
            table: "payment_attempts",
            column: "provider_return_reference",
            unique: true,
            filter: "provider_return_reference IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_traveller_snapshots_checkout_session_id_traveller_id",
            schema: "booking",
            table: "traveller_snapshots",
            columns: TravellerSnapshotIdentityColumns,
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "booking_recovery_cases",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "checkout_acceptances",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "checkout_revision_components",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "component_bookings",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "idempotency_records",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "payment_attempts",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "traveller_snapshots",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "checkout_revisions",
            schema: "booking");

        migrationBuilder.DropTable(
            name: "checkout_sessions",
            schema: "booking");
    }
}
