using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReturnLoad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class M6_Tracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LoadId",
                table: "Trips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "BatteryLevel",
                table: "TrackingEvents",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "TrackingEvents",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Trips_LoadId",
                table: "Trips",
                column: "LoadId");

            migrationBuilder.AddForeignKey(
                name: "FK_Trips_Loads_LoadId",
                table: "Trips",
                column: "LoadId",
                principalTable: "Loads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Trips_Loads_LoadId",
                table: "Trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_LoadId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "LoadId",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "BatteryLevel",
                table: "TrackingEvents");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "TrackingEvents");
        }
    }
}
