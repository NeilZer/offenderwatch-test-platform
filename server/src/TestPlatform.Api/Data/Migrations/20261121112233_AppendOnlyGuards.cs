using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TestPlatform.Api.Data.Migrations
{
    /// <summary>
    /// TM-05 — append-only history enforced by the DATABASE itself, not only by application code:
    ///   • a finished run or a finished scenario result can never be updated;
    ///   • runs, results and evidence can never be deleted;
    ///   • evidence rows can never be updated.
    /// Anything that tries (a bug, a manual SQL edit, a future feature) is rejected with an error.
    /// </summary>
    public partial class AppendOnlyGuards : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_runs_frozen_after_finish BEFORE UPDATE ON Runs
                WHEN OLD.FinishedAt IS NOT NULL
                BEGIN SELECT RAISE(ABORT, 'Append-only: a finished run cannot be changed'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_runs_no_delete BEFORE DELETE ON Runs
                BEGIN SELECT RAISE(ABORT, 'Append-only: runs cannot be deleted'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_results_frozen_after_finish BEFORE UPDATE ON ScenarioResults
                WHEN OLD.FinishedAt IS NOT NULL
                BEGIN SELECT RAISE(ABORT, 'Append-only: a finished scenario result cannot be changed'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_results_no_delete BEFORE DELETE ON ScenarioResults
                BEGIN SELECT RAISE(ABORT, 'Append-only: scenario results cannot be deleted'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_evidence_no_update BEFORE UPDATE ON Evidence
                BEGIN SELECT RAISE(ABORT, 'Evidence is immutable'); END;
                """);
            migrationBuilder.Sql("""
                CREATE TRIGGER trg_evidence_no_delete BEFORE DELETE ON Evidence
                BEGIN SELECT RAISE(ABORT, 'Evidence is immutable'); END;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var trigger in new[]
            {
                "trg_runs_frozen_after_finish", "trg_runs_no_delete", "trg_results_frozen_after_finish",
                "trg_results_no_delete", "trg_evidence_no_update", "trg_evidence_no_delete",
            })
            {
                migrationBuilder.Sql($"DROP TRIGGER IF EXISTS {trigger};");
            }
        }
    }
}
