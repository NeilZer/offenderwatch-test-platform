using System.Net;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TestPlatform.Api.Data;
using TestPlatform.Api.Hubs;

namespace TestPlatform.Tests;

/// <summary>A real SQLite database in memory, created through the REAL migrations (incl. triggers).</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public TestDatabase()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = NewContext();
        db.Database.Migrate();
    }

    public PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options);

    public void Dispose() => connection.Dispose();
}

/// <summary>Records every HTTP call and answers from a script — stands in for the OffenderWatch API.</summary>
public sealed class FakeTargetApi : HttpMessageHandler, IHttpClientFactory
{
    public List<string> Calls { get; } = new();
    public Dictionary<string, (HttpStatusCode Status, string Body)> Responses { get; } = new();

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var key = $"{request.Method} {request.RequestUri!.AbsolutePath}";
        Calls.Add(key);
        var (status, body) = Responses.TryGetValue(key, out var r) ? r : (HttpStatusCode.NotFound, "");
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}

/// <summary>SignalR hub context that swallows every message (tests don't need real-time delivery).</summary>
public sealed class NullHubContext : IHubContext<RunsHub>
{
    public IHubClients Clients { get; } = new NullClients();
    public IGroupManager Groups { get; } = new NullGroups();

    private sealed class NullClients : IHubClients
    {
        private static readonly IClientProxy Proxy = new NullProxy();
        public IClientProxy All => Proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Client(string connectionId) => Proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy;
        public IClientProxy Group(string groupName) => Proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy;
        public IClientProxy User(string userId) => Proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy;
    }

    private sealed class NullProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class NullGroups : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
