using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using TestPlatform.Api.Data;
using TestPlatform.Api.Hubs;
using TestPlatform.Api.Infrastructure;
using TestPlatform.Api.Services;

// Utility: `dotnet run -- hash-password <password>` prints a hash for appsettings Auth:Users.
if (args.Length == 2 && args[0] == "hash-password")
{
    Console.WriteLine(PasswordHasher.Hash(args[1]));
    return;
}

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------------ configuration
builder.Services.Configure<PlatformOptions>(builder.Configuration.GetSection("Platform"));
builder.Services.Configure<SuiteOptions>(builder.Configuration.GetSection("Suite"));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));

// Storage locations are resolved ONCE and shared: database, evidence files, session keys.
var paths = new PlatformPaths(
    builder.Environment.ContentRootPath,
    builder.Configuration.GetSection("Platform").Get<PlatformOptions>() ?? new PlatformOptions(),
    builder.Configuration.GetSection("Suite").Get<SuiteOptions>() ?? new SuiteOptions());
builder.Services.AddSingleton(paths);

// ------------------------------------------------------------------ data (SQLite + EF Core)
builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseSqlite($"Data Source={paths.DatabaseFile};Default Timeout=30"));

// ------------------------------------------------------------------ services
builder.Services.AddSingleton<EvidenceStore>();
builder.Services.AddSingleton<RunEvents>();
builder.Services.AddSingleton<RunLauncher>();
builder.Services.AddSingleton<UserStore>();
builder.Services.AddScoped<RunService>();
builder.Services.AddScoped<RunOrchestrator>();
builder.Services.AddScoped<TestDataService>();
builder.Services.AddScoped<HistoryService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ComparisonService>();
builder.Services.AddScoped<ScheduleService>();
builder.Services.AddScoped<RegressionNotifier>();
builder.Services.AddHostedService<ScheduleRunner>();
builder.Services.AddHttpClient("target", c => c.Timeout = TimeSpan.FromSeconds(20));
builder.Services.AddHttpClient("webhook", c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSignalR();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddControllers();

// ------------------------------------------------------------------ auth (B-07)
// Session-cookie keys live next to the database, so sign-ins survive a restart or a new container.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(paths.DataDirectory, "keys")))
    .SetApplicationName("TestManagementPlatform");

builder.Services
    .AddAuthentication(AuthSetup.CookieScheme)
    .AddCookie(AuthSetup.CookieScheme, options =>
    {
        options.Cookie.Name = "tm_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        // An API answers 401/403 — it never redirects to a login page.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = 401; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = 403; return Task.CompletedTask; };
    })
    .AddScheme<AuthenticationSchemeOptions, RunnerTokenHandler>(AuthSetup.RunnerScheme, _ => { });

builder.Services.AddAuthorization(options =>
{
    // Default: every endpoint needs a signed-in person (viewer or operator).
    options.FallbackPolicy = new AuthorizationPolicyBuilder(AuthSetup.CookieScheme).RequireAuthenticatedUser().Build();
    options.AddPolicy(AuthSetup.OperatorPolicy, p => p.AddAuthenticationSchemes(AuthSetup.CookieScheme).RequireRole(Roles.Operator));
    options.AddPolicy(AuthSetup.RunnerPolicy, p => p.AddAuthenticationSchemes(AuthSetup.RunnerScheme).RequireRole("runner"));
});

// ------------------------------------------------------------------ OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Test Management Platform API",
        Version = "v1",
        Description = "Runs, live results, history, evidence and test data for the OffenderWatch suite. " +
                      "Sign in via POST /api/auth/login (cookie). Runner endpoints use header X-Platform-Token.",
    });
    c.AddSecurityDefinition("RunnerToken", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey, In = ParameterLocation.Header, Name = RunnerTokenHandler.HeaderName,
        Description = "Ingestion token (runner endpoints only)",
    });
});

var app = builder.Build();

// ------------------------------------------------------------------ database: migrate + recover
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    db.Database.Migrate();                                   // creates the schema from scratch on a clean machine
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");   // readers never block the ingesting writer
    await scope.ServiceProvider.GetRequiredService<RunOrchestrator>().RecoverInterruptedRunsAsync(CancellationToken.None);
}

// ------------------------------------------------------------------ pipeline
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(c => c.DocumentTitle = "Test Management Platform API");
app.UseDefaultFiles();
app.UseStaticFiles(); // serves the built React client from wwwroot (production / one-command start)
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<RunsHub>("/hubs/runs");
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous(); // client-side routing

app.Run();

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
