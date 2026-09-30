namespace SkillsAtlas;

internal sealed record SkillEntry(
    string RepositoryName,
    string RepositoryUrl,
    string Name,
    string ShortDescription,
    string RelativeFilePath,
    string CommitHash,
    string Content,
    string FileUrl);

internal sealed record RepositoryCheckout(
    string RootPath,
    string RepositoryName,
    string RepositoryUrl,
    string CommitHash,
    string? TemporaryDirectory) : IDisposable
{
    public static async Task<RepositoryCheckout> OpenAsync(string source)
    {
        string rootPath;
        string? temporaryDirectory = null;
        string? cloneSource = null;
        string remoteUrl;

        if (Directory.Exists(source))
        {
            rootPath = Path.GetFullPath(source);
            remoteUrl = await TryReadOriginAsync(rootPath) ?? new UriBuilder(Uri.UriSchemeFile, string.Empty)
                { Path = rootPath }.Uri.AbsoluteUri;
        }
        else
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), $"skillsatlas-{Guid.NewGuid():N}");
            Directory.CreateDirectory(temporaryDirectory);
            rootPath = Path.Combine(temporaryDirectory, "repository");
            cloneSource = source;
            remoteUrl = NormalizeRepositoryUrl(source);
        }

        try
        {
            if (cloneSource is not null)
                await RunGitAsync(temporaryDirectory!, "clone", "--depth", "1", "--no-tags", "--", cloneSource, rootPath);

            var commitHash = (await RunGitAsync(rootPath, "rev-parse", "HEAD")).Trim();
            if (commitHash.Length == 0)
                throw new InvalidOperationException("Git did not return a commit hash for the repository.");

            var repositoryName = GetRepositoryName(remoteUrl, rootPath);
            return new RepositoryCheckout(rootPath, repositoryName, NormalizeRepositoryUrl(remoteUrl), commitHash, temporaryDirectory);
        }
        catch
        {
            if (temporaryDirectory is not null)
                Directory.Delete(temporaryDirectory, recursive: true);
            throw;
        }
    }

    public string GetFileUrl(string relativePath)
    {
        if (Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var repositoryUri) &&
            repositoryUri.Scheme is "http" or "https")
        {
            var escapedPath = string.Join("/", relativePath.Split('/').Select(Uri.EscapeDataString));
            return $"{RepositoryUrl}/blob/{CommitHash}/{escapedPath}";
        }

        return new UriBuilder(Uri.UriSchemeFile, string.Empty)
            { Path = Path.GetFullPath(Path.Combine(RootPath, relativePath)) }.Uri.AbsoluteUri;
    }

    public void Dispose()
    {
        if (TemporaryDirectory is not null && Directory.Exists(TemporaryDirectory))
            Directory.Delete(TemporaryDirectory, recursive: true);
    }

    private static string GetRepositoryName(string remoteUrl, string rootPath)
    {
        string name;
        if (Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri))
            name = Path.GetFileName(uri.AbsolutePath.TrimEnd('/'));
        else
            name = Path.GetFileName(remoteUrl.TrimEnd('/').Replace(':', '/'));

        if (string.IsNullOrWhiteSpace(name))
            name = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar));
        return name.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    private static string NormalizeRepositoryUrl(string value)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith("git@", StringComparison.Ordinal) && normalized.Contains(':'))
        {
            var separator = normalized.IndexOf(':');
            normalized = $"https://{normalized[4..separator]}/{normalized[(separator + 1)..]}";
        }
        else if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri) &&
                 uri.Scheme is "ssh" or "git")
        {
            normalized = $"https://{uri.Authority}{uri.AbsolutePath}";
        }

        normalized = normalized.TrimEnd('/');
        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];
        return normalized;
    }

    private static async Task<string?> TryReadOriginAsync(string rootPath)
    {
        try
        {
            var origin = await RunGitAsync(rootPath, "remote", "get-url", "origin");
            return string.IsNullOrWhiteSpace(origin) ? null : origin.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string> RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start git. Install Git and try again.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error.Trim()}");
        return output;
    }
}

internal static class SkillScanner
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", "vendor", "target", "dist"
    };

    public static List<SkillEntry> Scan(RepositoryCheckout checkout)
    {
        var result = new List<SkillEntry>();
        var pending = new Stack<string>();
        pending.Push(checkout.RootPath);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                if (!IgnoredDirectories.Contains(Path.GetFileName(childDirectory)) &&
                    (File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) == 0)
                    pending.Push(childDirectory);
            }

            foreach (var filePath in Directory.EnumerateFiles(directory)
                         .Where(path => string.Equals(Path.GetFileName(path), "SKILL.md", StringComparison.OrdinalIgnoreCase) &&
                                        (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0))
            {
                var relativePath = Path.GetRelativePath(checkout.RootPath, filePath)
                    .Replace(Path.DirectorySeparatorChar, '/');
                var content = File.ReadAllText(filePath);
                var (name, description, detail) = ParseSkill(content, Path.GetDirectoryName(filePath));
                result.Add(new SkillEntry(
                    checkout.RepositoryName,
                    checkout.RepositoryUrl,
                    name,
                    description,
                    relativePath,
                    checkout.CommitHash,
                    detail,
                    checkout.GetFileUrl(relativePath)));
            }
        }

        return result.OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static (string Name, string Description, string Detail) ParseSkill(string content, string? parentDirectory)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var name = parentDirectory is null ? "Unnamed skill" : Path.GetFileName(parentDirectory);
        var description = string.Empty;
        var detailStart = 0;

        if (lines.Length > 0 && lines[0].Trim() == "---")
        {
            var end = Array.FindIndex(lines, 1, line => line.Trim() == "---");
            if (end >= 0)
            {
                for (var index = 1; index < end; index++)
                {
                    var line = lines[index].Trim();
                    if (line.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                        name = Unquote(line[5..].Trim());
                    else if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                    {
                        var value = line[12..].Trim();
                        if (value.StartsWith('>') || value.StartsWith('|'))
                        {
                            var blockLines = new List<string>();
                            var next = index + 1;
                            while (next < end && (string.IsNullOrWhiteSpace(lines[next]) || char.IsWhiteSpace(lines[next][0])))
                            {
                                if (!string.IsNullOrWhiteSpace(lines[next]))
                                    blockLines.Add(lines[next].Trim());
                                next++;
                            }
                            description = string.Join(value[0] == '>' ? " " : "\n", blockLines);
                            index = next - 1;
                        }
                        else
                            description = Unquote(value);
                    }
                }
                detailStart = end + 1;
            }
        }

        var detail = string.Join("\n", lines.Skip(detailStart)).Trim();
        if (string.IsNullOrWhiteSpace(description))
        {
            description = detail.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => !line.StartsWith('#'))?.Trim() ?? "No short description provided.";
        }

        if (string.IsNullOrWhiteSpace(name))
            name = parentDirectory is null ? "Unnamed skill" : Path.GetFileName(parentDirectory);
        return (name, description, detail);
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            return value[1..^1];
        return value;
    }
}
