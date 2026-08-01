using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReturnLoad.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoadPickupAreaType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing loads default to Suburban (10 km radius) — a safe middle tier (Part 2).
            migrationBuilder.AddColumn<string>(
                name: "PickupAreaType",
                table: "Loads",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Suburban");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PickupAreaType",
                table: "Loads");
        }
    }
}
