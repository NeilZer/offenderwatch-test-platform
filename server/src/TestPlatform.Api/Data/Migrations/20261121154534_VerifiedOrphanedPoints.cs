using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPlatform.Api.Data.Migrations
{
    /// <summary>
    /// TM-06 correction. Location points used to be marked "RemovedWithParent" by ASSUMPTION when their offender
    /// was deleted. On 21/11/2026 all 286 such records were checked against the live application
    /// (GET /api/offenders/{id}/trail of each deleted offender): every one of them was still returned — the app
    /// keeps orphaned points (BUG-02). They are relabelled accordingly. From now on the state is verified by the
    /// platform at the moment the offender is deleted (TestDataService.ResolveChildrenAsync).
    /// TestData is lifecycle data, not run history, so it is not covered by the append-only triggers.
    /// </summary>
    public partial class VerifiedOrphanedPoints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE TestData
                SET State = 'OrphanedInApplication',
                    Note  = 'Verified 21/11/2026: still returned by the application after its offender was deleted (BUG-02). The app has no endpoint to delete a single point.'
                WHERE Kind = 'LocationPoint' AND State = 'RemovedWithParent';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE TestData
                SET State = 'RemovedWithParent',
                    Note  = 'Removed together with its offender. Note: the application keeps orphaned points (BUG-02).'
                WHERE Kind = 'LocationPoint' AND State = 'OrphanedInApplication'
                  AND Note LIKE 'Verified 21/11/2026:%';
                """);
        }
    }
}
