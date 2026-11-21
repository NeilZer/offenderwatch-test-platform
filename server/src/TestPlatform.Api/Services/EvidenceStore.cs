using System.Security.Cryptography;
using TestPlatform.Api.Domain;
using TestPlatform.Api.Infrastructure;

namespace TestPlatform.Api.Services;

/// <summary>
/// Stores evidence bytes on disk (TM-08) — write-once:
///  - every file gets a NEW unique name (FileMode.CreateNew never overwrites),
///  - its SHA-256 is stored in SQLite, so any later tampering is detectable (see Verify),
///  - the file is marked read-only,
///  - there is no API to update or delete evidence, and DB triggers forbid it.
/// Layout: data/evidence/run-00012/result-00345/03-screenshot.png
/// </summary>
public class EvidenceStore(PlatformPaths paths, ILogger<EvidenceStore> logger)
{
    private const long MaxBytes = 50 * 1024 * 1024;

    public async Task<Evidence> SaveAsync(int runId, int resultId, EvidenceKind kind, string originalName, string contentType,
        Stream content, CancellationToken ct)
    {
        var directory = Path.Combine(paths.RunDirectory(runId), $"result-{resultId:D5}");
        Directory.CreateDirectory(directory);
        var index = Directory.GetFiles(directory).Length + 1;
        var safeName = $"{index:D2}-{SanitiseFileName(originalName)}";
        var fullPath = Path.Combine(directory, safeName);

        var (size, sha) = await WriteOnceAsync(fullPath, content, ct);
        File.SetAttributes(fullPath, File.GetAttributes(fullPath) | FileAttributes.ReadOnly);
        logger.LogDebug("Evidence stored {Path} ({Size} bytes)", fullPath, size);

        return new Evidence
        {
            ScenarioResultId = resultId,
            Kind = kind,
            FileName = originalName,
            ContentType = SafeContentType(contentType),
            RelativePath = Path.GetRelativePath(paths.EvidenceDirectory, fullPath).Replace('\\', '/'),
            SizeBytes = size,
            Sha256 = sha,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    /// <summary>
    /// Streams the upload to a NEW file while hashing it. If anything fails (disk full, client gone,
    /// size limit), the partial file is deleted, so a failed upload never leaves a half-written file behind.
    /// </summary>
    private async Task<(long Size, string Sha)> WriteOnceAsync(string fullPath, Stream content, CancellationToken ct)
    {
        try
        {
            await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long size = 0;
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                size += read;
                if (size > MaxBytes) throw new BadRequestException($"Evidence larger than {MaxBytes / 1024 / 1024} MB");
                hasher.AppendData(buffer, 0, read);
                await file.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            await file.FlushAsync(ct);
            return (size, Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant());
        }
        catch (Exception ex) when (ex is not IOException { HResult: unchecked((int)0x80070050) }) // 0x50 = file already exists: never touch it
        {
            TryDelete(fullPath);
            logger.LogWarning(ex, "Evidence write failed, partial file removed: {Path}", fullPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort — the error that matters is rethrown */ }
    }

    public string FullPath(Evidence evidence)
    {
        var full = Path.GetFullPath(Path.Combine(paths.EvidenceDirectory, evidence.RelativePath));
        // Defence in depth: never serve anything outside the evidence folder.
        if (!full.StartsWith(paths.EvidenceDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Evidence path escapes the evidence directory");
        return full;
    }

    /// <summary>True when the file on disk still has the hash recorded at upload time.</summary>
    public async Task<bool> VerifyAsync(Evidence evidence, CancellationToken ct)
    {
        var path = FullPath(evidence);
        if (!File.Exists(path)) return false;
        await using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
        return hash == evidence.Sha256;
    }

    /// <summary>
    /// Evidence is served from the platform's own origin, so its content type must never let a file
    /// run code there (e.g. an upload declared as text/html = stored XSS). Only inert types are kept;
    /// anything else is served as a download (application/octet-stream).
    /// </summary>
    private static readonly HashSet<string> InlineSafeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp",
        "text/plain", "text/markdown", "application/json", "application/zip",
    };

    public static string SafeContentType(string? declared)
    {
        var mediaType = declared?.Split(';')[0].Trim() ?? "";
        return InlineSafeTypes.Contains(mediaType) ? mediaType.ToLowerInvariant() : "application/octet-stream";
    }

    /// <summary>Only images and plain text are shown inline in the browser; everything else downloads.</summary>
    public static bool CanDisplayInline(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || contentType is "text/plain" or "text/markdown" or "application/json";

    public static EvidenceKind ParseKind(string? kind) =>
        Enum.TryParse<EvidenceKind>(kind, ignoreCase: true, out var parsed) ? parsed : EvidenceKind.Other;

    private static string SanitiseFileName(string name)
    {
        var cleaned = new string(Path.GetFileName(name).Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '-').ToArray());
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned.Trim('.').Length == 0) cleaned = "evidence.bin";
        return cleaned.Length > 80 ? cleaned[^80..] : cleaned;
    }
}
