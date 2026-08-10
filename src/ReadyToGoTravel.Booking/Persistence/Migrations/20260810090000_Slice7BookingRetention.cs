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
