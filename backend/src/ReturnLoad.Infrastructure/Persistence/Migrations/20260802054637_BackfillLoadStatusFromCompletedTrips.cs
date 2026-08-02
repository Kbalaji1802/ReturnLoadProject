using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReturnLoad.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Repairs loads left behind by the missing trip→load projection: until TripService began
    /// advancing the load alongside its trip, a load stopped at Booked when the booking was
    /// accepted and never moved again. Trips that completed before that fix will never advance
    /// again, so their loads cannot self-correct — the owner's dashboard would report a finished
    /// delivery as still running, permanently.
    ///
    /// Data-only: no model change, so the snapshot is untouched.
    /// </summary>
    public partial class BackfillLoadStatusFromCompletedTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Statuses persist as enum member names (HasConversion<string>), not ordinals.
            // Only Booked/InTransit are touched: a load already Delivered is correct, and a
            // Cancelled one must not be resurrected by a trip that happens to read Completed.
            migrationBuilder.Sql("""
                UPDATE "Loads" AS l
                SET "Status" = 'Delivered'
                FROM "Trips" AS t
                WHERE t."LoadId" = l."Id"
                  AND t."Status" = 'Completed'
                  AND l."Status" IN ('Booked', 'InTransit')
                  AND l."IsDeleted" = false
                  AND t."IsDeleted" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty. Each repaired row was Booked or InTransit and the original value
            // is not recorded, so reverting would have to guess — and guessing wrong reintroduces
            // exactly the inconsistency this removes.
        }
    }
}
