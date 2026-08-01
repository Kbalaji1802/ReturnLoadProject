using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReturnLoad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DriverAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing drivers default to Offline — they must opt in to Available before being
            // matched (Part 3). New rows get their value from the domain (also Offline on register).
            migrationBuilder.AddColumn<string>(
                name: "Availability",
                table: "Drivers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Offline");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_Availability",
                table: "Drivers",
                column: "Availability");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Drivers_Availability",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "Availability",
                table: "Drivers");
        }
    }
}
