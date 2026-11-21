using Microsoft.Extensions.Logging.Abstractions;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Infrastructure;
using TestPlatform.Api.Services;

namespace TestPlatform.Tests;

/// <summary>TM-08 — evidence files are written once, hashed, read-only, and never left half-written.</summary>
public sealed class EvidenceStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "tm-evidence-" + Guid.NewGuid().ToString("N"));
    private readonly EvidenceStore store;

    public EvidenceStoreTests()
    {
        var paths = new PlatformPaths(root, new PlatformOptions { DataDirectory = "data" }, new SuiteOptions());
        store = new EvidenceStore(paths, NullLogger<EvidenceStore>.Instance);
    }

    [Fact]
    public async Task A_stored_file_is_read_only_and_its_hash_verifies()
    {
        var evidence = await store.SaveAsync(1, 7, EvidenceKind.Log, "execution.log", "text/plain", new MemoryStream("hello"u8.ToArray()), default);

        var path = store.FullPath(evidence);
        Assert.True(File.GetAttributes(path).HasFlag(FileAttributes.ReadOnly));
        Assert.Equal(5, evidence.SizeBytes);
        Assert.True(await store.VerifyAsync(evidence, default));
    }

    [Fact]
    public async Task A_failed_upload_leaves_no_partial_file_behind()
    {
        // Simulates the disk filling up / the client disconnecting half-way through the upload.
        var failing = new FailingStream(bytesBeforeFailure: 100_000);

        await Assert.ThrowsAsync<IOException>(() =>
            store.SaveAsync(1, 8, EvidenceKind.Screenshot, "shot.png", "image/png", failing, default));

        var resultDir = Path.Combine(root, "data", "evidence", "run-00001", "result-00008");
        Assert.Empty(Directory.Exists(resultDir) ? Directory.GetFiles(resultDir) : []);
    }

    [Fact]
    public async Task An_html_upload_is_stored_as_a_harmless_download_type()
    {
        var evidence = await store.SaveAsync(1, 9, EvidenceKind.Other, "x.html", "text/html", new MemoryStream("<script>"u8.ToArray()), default);
        Assert.Equal("application/octet-stream", evidence.ContentType);
    }

    public void Dispose()
    {
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(root, recursive: true);
    }

    /// <summary>Returns data, then throws like a full disk / broken connection would.</summary>
    private sealed class FailingStream(int bytesBeforeFailure) : Stream
    {
        private int sent;
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (sent >= bytesBeforeFailure) throw new IOException("There is not enough space on the disk.");
            var n = Math.Min(count, bytesBeforeFailure - sent);
            Array.Fill(buffer, (byte)1, offset, n);
            sent += n;
            return n;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => sent; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
