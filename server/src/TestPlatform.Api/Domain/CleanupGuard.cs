namespace TestPlatform.Api.Domain;

/// <summary>
/// TM-06 safety rules for "Clean test data". Pure logic, unit-tested (B-06).
///
/// A record may be deleted from the target application ONLY if ALL of these hold:
///   1. it was registered by a run of this platform (the caller only passes registered records);
///   2. it is still waiting for clean-up: Active, or CleanupFailed (a failed attempt is retried);
///   3. its id is not a protected seeded id (the original 11 offenders);
///   4. the live record still has the same national ID that was registered — so an id that the
///      application re-used for someone else is never deleted.
/// </summary>
public static class CleanupGuard
{
    public enum Decision { Delete, SkipProtected, SkipNotActive, SkipAlreadyGone, SkipIdentityMismatch, SkipNotDeletable }

    public static Decision Decide(TestDataRecord record, IReadOnlySet<string> protectedIds, string? liveNaturalKey, bool liveRecordExists)
    {
        if (!IsWaitingForCleanup(record.State)) return Decision.SkipNotActive;
        if (protectedIds.Contains(record.ExternalId)) return Decision.SkipProtected;
        if (record.Kind != TestDataKind.Offender) return Decision.SkipNotDeletable; // points have no delete endpoint
        if (!liveRecordExists) return Decision.SkipAlreadyGone;
        if (!string.Equals(record.NaturalKey, liveNaturalKey, StringComparison.Ordinal)) return Decision.SkipIdentityMismatch;
        return Decision.Delete;
    }

    /// <summary>Records still present in the application: never cleaned, or a previous attempt failed.</summary>
    public static bool IsWaitingForCleanup(TestDataState state) =>
        state is TestDataState.Active or TestDataState.CleanupFailed;

    /// <summary>Parses "1-11,15" into the set {"1",...,"11","15"}.</summary>
    public static IReadOnlySet<string> ParseProtectedIds(string? spec)
    {
        var set = new HashSet<string>();
        if (string.IsNullOrWhiteSpace(spec)) return set;
        foreach (var part in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var range = part.Split('-', StringSplitOptions.TrimEntries);
            if (range.Length == 2 && int.TryParse(range[0], out var from) && int.TryParse(range[1], out var to))
            {
                for (var i = from; i <= to; i++) set.Add(i.ToString());
            }
            else if (int.TryParse(part, out var single))
            {
                set.Add(single.ToString());
            }
            else
            {
                throw new FormatException($"Invalid protected id spec part: '{part}'");
            }
        }
        return set;
    }
}
