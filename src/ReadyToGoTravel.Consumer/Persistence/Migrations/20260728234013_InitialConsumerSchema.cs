using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReadyToGoTravel.Consumer.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialConsumerSchema : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "consumer");

        migrationBuilder.CreateTable(
            name: "customers",
            schema: "consumer",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                preferred_locale = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                display_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                adult_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                adult_policy_version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_customers", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "travellers",
            schema: "consumer",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                given_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                family_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                relationship_label = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                is_minor = table.Column<bool>(type: "boolean", nullable: false),
                guardian_authority_confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_travellers", x => x.id);
                table.ForeignKey(
                    name: "FK_travellers_customers_customer_id",
                    column: x => x.customer_id,
                    principalSchema: "consumer",
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "trips",
            schema: "consumer",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                primary_destination = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                start_date = table.Column<DateOnly>(type: "date", nullable: true),
                end_date = table.Column<DateOnly>(type: "date", nullable: true),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_trips", x => x.id);
                table.CheckConstraint("ck_trips_dates", "end_date IS NULL OR start_date IS NULL OR end_date >= start_date");
                table.ForeignKey(
                    name: "FK_trips_customers_customer_id",
                    column: x => x.customer_id,
                    principalSchema: "consumer",
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_customers_subject",
            schema: "consumer",
            table: "customers",
            column: "subject",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_travellers_customer_id",
            schema: "consumer",
            table: "travellers",
            column: "customer_id");

        migrationBuilder.CreateIndex(
            name: "IX_trips_customer_id",
            schema: "consumer",
            table: "trips",
            column: "customer_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "travellers",
            schema: "consumer");

        migrationBuilder.DropTable(
            name: "trips",
            schema: "consumer");

        migrationBuilder.DropTable(
            name: "customers",
            schema: "consumer");
    }
}
