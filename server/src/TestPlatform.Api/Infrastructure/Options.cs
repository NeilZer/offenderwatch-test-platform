namespace TestPlatform.Api.Infrastructure;

/// <summary>"Platform" section of appsettings.json.</summary>
public class PlatformOptions
{
    /// <summary>URL at which the suite's reporter reaches this API (passed to the child process).</summary>
    public string PublicUrl { get; set; } = "";
    /// <summary>Folder for platform.db and the evidence files; relative to the API project folder.</summary>
    public string DataDirectory { get; set; } = "data";
    /// <summary>Shared secret the runner must send in X-Platform-Token when posting results.</summary>
    public string IngestToken { get; set; } = "";
}

/// <summary>"Suite" section: where the Part 3 automation lives and how to start it.</summary>
public class SuiteOptions
{
    /// <summary>Explicit path to the suite. When empty or missing, the candidates below are tried.</summary>
    public string WorkingDirectory { get; set; } = "";
    /// <summary>
    /// Where the suite may live relative to the API project, in order: "SuperCom" (the original layout) and
    /// "offenderwatch-qa-suite" (the name a plain `git clone` gives). The first folder containing
    /// playwright.config.ts wins, so the platform works with either layout without configuration.
    /// </summary>
    public List<string> WorkingDirectoryCandidates { get; set; } = new();
    /// <summary>Command line without project filter, e.g. "npx playwright test".</summary>
    public string Command { get; set; } = "npx playwright test";
    /// <summary>Scope name → extra CLI arguments, e.g. "api" → "--project=api".</summary>
    public Dictionary<string, string> Scopes { get; set; } = new();
}

/// <summary>"Auth" section (B-07): demo users with PBKDF2 password hashes.</summary>
public class AuthOptions
{
    public List<UserOptions> Users { get; set; } = new();
}

public class UserOptions
{
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Viewer;
}

public static class Roles
{
    public const string Viewer = "viewer";
    public const string Operator = "operator";
}

/// <summary>Resolved absolute paths, computed once at startup.</summary>
public class PlatformPaths
{
    public PlatformPaths(string contentRoot, PlatformOptions platform, SuiteOptions suite)
    {
        DataDirectory = Path.GetFullPath(Path.Combine(contentRoot, platform.DataDirectory));
        EvidenceDirectory = Path.Combine(DataDirectory, "evidence");
        DatabaseFile = Path.Combine(DataDirectory, "platform.db");
        SuiteDirectory = ResolveSuiteDirectory(contentRoot, suite);
        Directory.CreateDirectory(EvidenceDirectory);
    }

    public string DataDirectory { get; }
    public string EvidenceDirectory { get; }
    public string DatabaseFile { get; }
    public string SuiteDirectory { get; }

    /// <summary>The configured folder if it exists, else the first candidate that contains a Playwright suite.</summary>
    public static string ResolveSuiteDirectory(string contentRoot, SuiteOptions suite)
    {
        var options = new[] { suite.WorkingDirectory }.Concat(suite.WorkingDirectoryCandidates)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.GetFullPath(Path.Combine(contentRoot, p)))
            .ToList();
        return options.FirstOrDefault(p => File.Exists(Path.Combine(p, "playwright.config.ts")))
               ?? options.FirstOrDefault() ?? "";
    }

    public string RunDirectory(int runId) => Path.Combine(EvidenceDirectory, $"run-{runId:D5}");
    public string RunnerOutputFile(int runId) => Path.Combine(RunDirectory(runId), "runner-output.log");
}
