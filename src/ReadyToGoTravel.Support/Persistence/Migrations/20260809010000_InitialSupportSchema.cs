using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyToGoTravel.Support.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialSupportSchema : Migration
{
    private static readonly string[] ScanWorkScheduleColumns = ["status", "next_attempt_at"];
    private static readonly string[] NotificationScheduleColumns = ["status", "not_before"];
    private static readonly string[] TicketMessageSequenceColumns = ["ticket_id", "sequence_number"];
    private static readonly string[] TicketStatusCreatedAtColumns = ["status", "created_at"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "support");

        migrationBuilder.CreateTable(
            name: "support_attachments",
            schema: "support",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                original_file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                content_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                size_bytes = table.Column<long>(type: "bigint", nullable: false),
                storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                sha256_checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                uploader_customer_subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                uploader_guest_token_id = table.Column<Guid>(type: "uuid", nullable: true),
                scan_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_support_attachments", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "support_audit_events",
            schema: "support",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ticket_id = table.Column<Guid>(type: "uuid", nullable: true),
                event_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                detail = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_support_audit_events", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "support_guest_access_tokens",
            schema: "support",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                last_used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                rotated_from_token_id = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_support_guest_access_tokens", x => x.id);
                table.ForeignKey(
                    name: "FK_support_guest_access_tokens_support_guest_access_tokens_rot~",
                    column: x => x.rotated_from_token_id,
                    principalSchema: "support",
                    principalTable: "support_guest_access_tokens",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "support_notification_outbox",
            schema: "support",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                dedupe_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                recipient_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                template = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
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
                table.PrimaryKey("PK_support_notification_outbox", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "support_tickets",
            schema: "support",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                booking_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_support_tickets", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "attachment_scan_work",
            schema: "support",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                attachment_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                attempts = table.Column<int>(type: "integer", nullable: false),
                next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                lease_owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                lease_expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                error_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_attachment_scan_work", x => x.id);
                table.ForeignKey(
                    name: "FK_attachment_scan_work_support_attachments_attachment_id",
                    column: x => x.attachment_id,
                    principalSchema: "support",
                    principalTable: "support_attachments",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "support_ticket_attachment_usage",
            schema: "support",
            columns: table => new
            {
                ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                bytes_used = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_support_ticket_attachment_usage", x => x.ticket_id);
                table.ForeignKey(
                    name: "FK_support_ticket_attachment_usage_support_tickets_ticket_id",
                    column: x => x.ticket_id,
                    principalSchema: "support",
                    principalTable: "support_tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "support_ticket_messages",
            schema: "support",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                ticket_id = table.Column<Guid>(type: "uuid", nullable: false),
                sequence_number = table.Column<int>(type: "integer", nullable: false),
                author_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                author_subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_support_ticket_messages", x => x.id);
                table.ForeignKey(
                    name: "FK_support_ticket_messages_support_tickets_ticket_id",
                    column: x => x.ticket_id,
                    principalSchema: "support",
                    principalTable: "support_tickets",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "support_message_attachment_usage",
            schema: "support",
            columns: table => new
            {
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                file_count = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_support_message_attachment_usage", x => x.message_id);
                table.ForeignKey(
                    name: "FK_support_message_attachment_usage_support_ticket_messages_me~",
                    column: x => x.message_id,
                    principalSchema: "support",
                    principalTable: "support_ticket_messages",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_attachment_scan_work_attachment_id",
            schema: "support",
            table: "attachment_scan_work",
            column: "attachment_id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_attachment_scan_work_status_next_attempt_at",
            schema: "support",
            table: "attachment_scan_work",
            columns: ScanWorkScheduleColumns);

        migrationBuilder.CreateIndex(
            name: "IX_support_attachments_message_id",
            schema: "support",
            table: "support_attachments",
            column: "message_id");

        migrationBuilder.CreateIndex(
            name: "IX_support_attachments_storage_key",
            schema: "support",
            table: "support_attachments",
            column: "storage_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_support_attachments_ticket_id",
            schema: "support",
            table: "support_attachments",
            column: "ticket_id");

        migrationBuilder.CreateIndex(
            name: "IX_support_audit_events_ticket_id",
            schema: "support",
            table: "support_audit_events",
            column: "ticket_id");

        migrationBuilder.CreateIndex(
            name: "IX_support_guest_access_tokens_rotated_from_token_id",
            schema: "support",
            table: "support_guest_access_tokens",
            column: "rotated_from_token_id");

        migrationBuilder.CreateIndex(
            name: "ix_support_guest_access_tokens_ticket_id_active",
            schema: "support",
            table: "support_guest_access_tokens",
            column: "ticket_id",
            unique: true,
            filter: "revoked_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_support_guest_access_tokens_token_hash",
            schema: "support",
            table: "support_guest_access_tokens",
            column: "token_hash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_support_notification_outbox_dedupe_key",
            schema: "support",
            table: "support_notification_outbox",
            column: "dedupe_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_support_notification_outbox_status_not_before",
            schema: "support",
            table: "support_notification_outbox",
            columns: NotificationScheduleColumns);

        migrationBuilder.CreateIndex(
            name: "IX_support_notification_outbox_ticket_id",
            schema: "support",
            table: "support_notification_outbox",
            column: "ticket_id");

        migrationBuilder.CreateIndex(
            name: "IX_support_ticket_messages_ticket_id_sequence_number",
            schema: "support",
            table: "support_ticket_messages",
            columns: TicketMessageSequenceColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_support_tickets_customer_subject",
            schema: "support",
            table: "support_tickets",
            column: "customer_subject");

        migrationBuilder.CreateIndex(
            name: "IX_support_tickets_status_created_at",
            schema: "support",
            table: "support_tickets",
            columns: TicketStatusCreatedAtColumns);

        migrationBuilder.Sql("""
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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS reject_support_ticket_message_mutation ON support.support_ticket_messages;
            DROP FUNCTION IF EXISTS support.reject_support_ticket_message_mutation();
            """);

        migrationBuilder.DropTable(
            name: "attachment_scan_work",
            schema: "support");

        migrationBuilder.DropTable(
            name: "support_audit_events",
            schema: "support");

        migrationBuilder.DropTable(
            name: "support_guest_access_tokens",
            schema: "support");

        migrationBuilder.DropTable(
            name: "support_message_attachment_usage",
            schema: "support");

        migrationBuilder.DropTable(
            name: "support_notification_outbox",
            schema: "support");

        migrationBuilder.DropTable(
            name: "support_ticket_attachment_usage",
            schema: "support");

        migrationBuilder.DropTable(
            name: "support_attachments",
            schema: "support");

        migrationBuilder.DropTable(
            name: "support_ticket_messages",
            schema: "support");

        migrationBuilder.DropTable(
            name: "support_tickets",
            schema: "support");
    }
}
