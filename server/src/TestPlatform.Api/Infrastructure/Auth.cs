using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TestPlatform.Api.Infrastructure;

/// <summary>
/// B-07 — minimal authentication.
///  - People sign in with a username/password (cookie session). Two roles:
///      viewer   → can watch runs, history, dashboard, evidence
///      operator → can also start/stop runs, edit environments, schedules, settings, and clean test data
///  - The test runner authenticates with a shared token (X-Platform-Token) on the ingestion endpoints only.
/// Users are configured in appsettings.json with PBKDF2 hashes (never plain text).
/// </summary>
public static class AuthSetup
{
    public const string CookieScheme = "PlatformCookie";
    public const string RunnerScheme = "RunnerToken";
    public const string OperatorPolicy = "Operator";
    public const string RunnerPolicy = "Runner";
}

public class UserStore(IOptions<AuthOptions> options)
{
    public UserOptions? Validate(string username, string password)
    {
        var user = options.Value.Users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        return user is not null && PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
    }
}

/// <summary>PBKDF2-SHA256, format: pbkdf2$&lt;iterations&gt;$&lt;salt b64&gt;$&lt;hash b64&gt;.</summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

/// <summary>Authenticates the test runner by the shared ingestion token.</summary>
public class RunnerTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<PlatformOptions> platform)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string HeaderName = "X-Platform-Token";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var supplied)) return Task.FromResult(AuthenticateResult.NoResult());
        var expected = platform.Value.IngestToken;
        if (string.IsNullOrEmpty(expected)) return Task.FromResult(AuthenticateResult.Fail("Ingestion token is not configured"));
        var ok = CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(supplied.ToString()), System.Text.Encoding.UTF8.GetBytes(expected));
        if (!ok) return Task.FromResult(AuthenticateResult.Fail("Invalid ingestion token"));
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "runner"), new Claim(ClaimTypes.Role, "runner")], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
