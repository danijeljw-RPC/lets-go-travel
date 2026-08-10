using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyToGoTravel.Booking.Persistence.Migrations;

/// <inheritdoc />
public partial class Slice7BookingRetention : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No backfill statement is needed here: this repository has never been deployed (per
        // explicit confirmation while resolving github issue #15 from the Codex review of PR #12),
        // so every environment applies this migration to an empty webhook_inbox table - there is
        // no pre-existing "Completed" row this column could ever need to retroactively populate.
        // See WebhookInboxItem.ComponentBookingId for why the application code is also safe
        // regardless: correlation is set synchronously, before an item ever reaches Completed.
        migrationBuilder.AddColumn<Guid>(
            name: "component_booking_id",
            schema: "booking",
            table: "webhook_inbox",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_webhook_inbox_component_booking_id",
            schema: "booking",
            table: "webhook_inbox",
            column: "component_booking_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_webhook_inbox_component_booking_id",
            schema: "booking",
            table: "webhook_inbox");

        migrationBuilder.DropColumn(
            name: "component_booking_id",
            schema: "booking",
            table: "webhook_inbox");
    }
}
