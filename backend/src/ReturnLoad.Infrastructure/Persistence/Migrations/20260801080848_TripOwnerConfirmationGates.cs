using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReturnLoad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TripOwnerConfirmationGates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeliveryAutoConfirmed",
                table: "Trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "PickupAutoConfirmed",
                table: "Trips",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusChangedAtUtc",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryAutoConfirmed",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "PickupAutoConfirmed",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                table: "Trips");
        }
    }
}
