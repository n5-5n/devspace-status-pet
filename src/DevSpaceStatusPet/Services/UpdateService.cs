using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevSpaceStatusPet.Services;

public sealed record UpdateRelease(
    string Version,
    string TagName,
    string Name,
    string ReleaseUrl,
    string Notes,
    bool IsPrerelease,
    DateTimeOffset? PublishedAt,
    string ZipUrl,
    string Sha256Url,
    long ZipSize);

public sealed record UpdateProgress(string Stage, int Percentage, string Detail);

internal readonly record struct SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    string? Prerelease) : IComparable<SemanticVersion>
{
    public bool IsPrerelease => !string.IsNullOrWhiteSpace(Prerelease);

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var metadataIndex = value.IndexOf('+');
        if (metadataIndex >= 0)
        {
            value = value[..metadataIndex];
        }

        string? prerelease = null;
        var prereleaseIndex = value.IndexOf('-');
        if (prereleaseIndex >= 0)
        {
            prerelease = value[(prereleaseIndex + 1)..];
            value = value[..prereleaseIndex];
        }

        var parts = value.Split('.');
        var patch = 0;
        if (parts.Length is < 2 or > 4 ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            (parts.Length >= 3 && !int.TryParse(parts[2], out patch)))
        {
            return false;
        }

        version = new SemanticVersion(major, minor, patch, prerelease);
        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0) return result;
        result = Minor.CompareTo(other.Minor);
        if (result != 0) return result;
        result = Patch.CompareTo(other.Patch);
        if (result != 0) return result;

        if (!IsPrerelease && !other.IsPrerelease) return 0;
        if (!IsPrerelease) return 1;
        if (!other.IsPrerelease) return -1;
        return ComparePrerelease(Prerelease!, other.Prerelease!);
    }

    public override string ToString() =>
        $"{Major}.{Minor}.{Patch}{(IsPrerelease ? $"-{Prerelease}" : string.Empty)}";

    private static int ComparePrerelease(string left, string right)
    {
        var leftParts = left.Split('.');
        var rightParts = right.Split('.');
        var length = Math.Max(leftParts.Length, rightParts.Length);
        for (var index = 0; index < length; index++)
        {
            if (index >= leftParts.Length) return -1;
            if (index >= rightParts.Length) return 1;

            var leftNumeric = int.TryParse(leftParts[index], out var leftNumber);
            var rightNumeric = int.TryParse(rightParts[index], out var rightNumber);
            int result;
            if (leftNumeric && rightNumeric)
            {
                result = leftNumber.CompareTo(rightNumber);
            }
            else if (leftNumeric)
            {
                result = -1;
            }
            else if (rightNumeric)
            {
                result = 1;
            }
            else
            {
                result = string.Compare(leftParts[index], rightParts[index], StringComparison.OrdinalIgnoreCase);
            }

            if (result != 0) return result;
        }

        return 0;
    }
}

public sealed class UpdateService : IDisposable
{
    private const string ReleasesEndpoint =
        "https://api.github.com/repos/n5-5n/devspace-status-pet/releases?per_page=30";
    private static readonly Regex Sha256Pattern =
        new(@"\b[0-9a-fA-F]{64}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient _client;
    private readonly bool _ownsClient;
    private readonly SemanticVersion _currentVersion;

    public UpdateService(string? currentVersion = null, HttpClient? client = null)
    {
        CurrentVersion = NormalizeCurrentVersion(currentVersion ?? ResolveAssemblyVersion());
        if (!SemanticVersion.TryParse(CurrentVersion, out _currentVersion))
        {
            throw new InvalidOperationException($"Invalid application version: {CurrentVersion}");
        }

        _client = client ?? new HttpClient();
        _ownsClient = client is null;
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
        {
            _client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("DevSpaceStatusPet", CurrentVersion));
        }
        _client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _client.Timeout = TimeSpan.FromMinutes(5);
    }

    public string CurrentVersion { get; }

    public async Task<UpdateRelease?> CheckAsync(
        bool includePrereleases,
        CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(ReleasesEndpoint, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return SelectLatestRelease(document.RootElement, _currentVersion, includePrereleases);
    }

    public async Task<string> PrepareInstallerAsync(
        UpdateRelease release,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var updateRoot = Path.Combine(
            Path.GetTempPath(),
            "DevSpaceStatusPetUpdate",
            $"{release.Version}-{Guid.NewGuid():N}");
        var zipPath = Path.Combine(updateRoot, "package.zip");
        var extractPath = Path.Combine(updateRoot, "extract");
        Directory.CreateDirectory(updateRoot);

        try
        {
            progress?.Report(new UpdateProgress("checksum", 2, "Downloading SHA-256"));
            var checksumText = await _client.GetStringAsync(release.Sha256Url, cancellationToken).ConfigureAwait(false);
            var expectedHash = ParseSha256(checksumText);

            progress?.Report(new UpdateProgress("download", 5, "Downloading update package"));
            await DownloadFileAsync(release.ZipUrl, zipPath, release.ZipSize, progress, cancellationToken)
                .ConfigureAwait(false);

            progress?.Report(new UpdateProgress("verify", 82, "Verifying SHA-256"));
            var actualHash = await ComputeSha256Async(zipPath, cancellationToken).ConfigureAwait(false);
            if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"SHA-256 mismatch. Expected {expectedHash}, actual {actualHash}.");
            }

            progress?.Report(new UpdateProgress("extract", 88, "Extracting update"));
            Directory.CreateDirectory(extractPath);
            ExtractZipSafely(zipPath, extractPath);

            var executable = Directory.EnumerateFiles(
                    extractPath,
                    "DevSpaceStatusPet.exe",
                    SearchOption.AllDirectories)
                .SingleOrDefault()
                ?? throw new InvalidDataException("DevSpaceStatusPet.exe was not found in the update package.");

            ValidateInstallerVersion(executable, release.Version);
            progress?.Report(new UpdateProgress("ready", 100, "Update is ready"));
            return executable;
        }
        catch
        {
            TryDeleteDirectory(updateRoot);
            throw;
        }
    }

    public static Process LaunchInstaller(string executablePath)
    {
        return Process.Start(new ProcessStartInfo(
                   executablePath,
                   "--install --silent --cleanup-source")
               {
                   UseShellExecute = true,
                   WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Path.GetTempPath()
               })
               ?? throw new InvalidOperationException("Could not start the update installer.");
    }

    internal static UpdateRelease? SelectLatestRelease(
        JsonElement releases,
        SemanticVersion currentVersion,
        bool includePrereleases)
    {
        if (releases.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("GitHub releases response was not an array.");
        }

        var candidates = new List<(SemanticVersion Version, UpdateRelease Release)>();
        foreach (var item in releases.EnumerateArray())
        {
            if (GetBoolean(item, "draft") ||
                (!includePrereleases && GetBoolean(item, "prerelease")) ||
                !TryGetString(item, "tag_name", out var tagName) ||
                !SemanticVersion.TryParse(tagName, out var version) ||
                version.CompareTo(currentVersion) <= 0)
            {
                continue;
            }

            var isPrerelease = GetBoolean(item, "prerelease") || version.IsPrerelease;
            if (!includePrereleases && isPrerelease)
            {
                continue;
            }

            var versionText = version.ToString();
            var expectedZip = $"DevSpace-Status-Pet-v{versionText}-win-x64.zip";
            var expectedSha = $"{expectedZip}.sha256";
            string? zipUrl = null;
            string? shaUrl = null;
            long zipSize = 0;
            if (item.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!TryGetString(asset, "name", out var assetName) ||
                        !TryGetString(asset, "browser_download_url", out var assetUrl))
                    {
                        continue;
                    }

                    if (assetName.Equals(expectedZip, StringComparison.OrdinalIgnoreCase))
                    {
                        zipUrl = assetUrl;
                        if (asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var size))
                        {
                            zipSize = size;
                        }
                    }
                    else if (assetName.Equals(expectedSha, StringComparison.OrdinalIgnoreCase))
                    {
                        shaUrl = assetUrl;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(zipUrl) || string.IsNullOrWhiteSpace(shaUrl))
            {
                continue;
            }

            DateTimeOffset? publishedAt = null;
            if (TryGetString(item, "published_at", out var publishedText) &&
                DateTimeOffset.TryParse(publishedText, out var published))
            {
                publishedAt = published;
            }

            candidates.Add((version, new UpdateRelease(
                versionText,
                tagName,
                TryGetString(item, "name", out var name) ? name : $"DevSpace Status Pet v{versionText}",
                TryGetString(item, "html_url", out var htmlUrl) ? htmlUrl : string.Empty,
                TryGetString(item, "body", out var body) ? body : string.Empty,
                isPrerelease,
                publishedAt,
                zipUrl,
                shaUrl,
                zipSize)));
        }

        return candidates
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Release)
            .FirstOrDefault();
    }

    internal static string ParseSha256(string checksumText)
    {
        var match = Sha256Pattern.Match(checksumText ?? string.Empty);
        if (!match.Success)
        {
            throw new InvalidDataException("The SHA-256 file did not contain a valid checksum.");
        }
        return match.Value.ToLowerInvariant();
    }

    internal static void ExtractZipSafely(string zipPath, string destinationPath)
    {
        var destinationRoot = Path.GetFullPath(destinationPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var targetPath = Path.GetFullPath(Path.Combine(destinationPath, entry.FullName));
            if (!targetPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Unsafe ZIP entry: {entry.FullName}");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, true);
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
    }

    private async Task DownloadFileAsync(
        string url,
        string destinationPath,
        long expectedSize,
        IProgress<UpdateProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _client.GetAsync(
                url,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var contentLength = response.Content.Headers.ContentLength ?? expectedSize;
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            true);

        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
            var percentage = contentLength > 0
                ? Math.Clamp(5 + (int)Math.Round(total * 75d / contentLength), 5, 80)
                : 40;
            progress?.Report(new UpdateProgress(
                "download",
                percentage,
                contentLength > 0
                    ? $"{FormatBytes(total)} / {FormatBytes(contentLength)}"
                    : FormatBytes(total)));
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ValidateInstallerVersion(string executablePath, string expectedVersion)
    {
        var productVersion = FileVersionInfo.GetVersionInfo(executablePath).ProductVersion;
        if (!SemanticVersion.TryParse(productVersion, out var actual) ||
            !SemanticVersion.TryParse(expectedVersion, out var expected) ||
            actual.CompareTo(expected) != 0)
        {
            throw new InvalidDataException(
                $"Update executable version mismatch. Expected {expectedVersion}, actual {productVersion ?? "unknown"}.");
        }
    }

    private static string ResolveAssemblyVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        return informational ?? assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string NormalizeCurrentVersion(string value)
    {
        var metadata = value.IndexOf('+');
        return (metadata >= 0 ? value[..metadata] : value).TrimStart('v', 'V');
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = property.GetString() ?? string.Empty;
        return true;
    }

    private static bool GetBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.True;

    private static string FormatBytes(long bytes)
    {
        const double megabyte = 1024d * 1024d;
        return bytes >= megabyte
            ? $"{bytes / megabyte:0.0} MB"
            : $"{bytes / 1024d:0.0} KB";
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Temporary update files are safe to leave for Windows cleanup.
        }
    }
}
