namespace SkillsAtlas;

public sealed record RepositorySkills(
    string RepositoryName,
    string RepositoryUrl,
    string CommitHash,
    IReadOnlyList<SkillEntry> Skills,
    string DatabasePath);

public sealed record SkillsLibrary(
    IReadOnlyList<SkillEntry> Skills,
    string DatabasePath);

public sealed class SkillsCatalog
{
    private readonly string _databasePath;
    private readonly SemaphoreSlim _databaseLock = new(1, 1);

    public SkillsCatalog(string? databasePath = null)
    {
        _databasePath = Path.GetFullPath(databasePath ?? GetDefaultDatabasePath());

        // Opening the database initializes the schema and creates the file before
        // the first repository is scanned, so both hosts have a durable store ready.
        using var database = new SkillsDatabase(_databasePath);
    }

    public static string GetDefaultDatabasePath()
    {
        var configuredPath = Environment.GetEnvironmentVariable("SKILLS_ATLAS_DATABASE");
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return Path.GetFullPath(configuredPath);

        var solutionDirectory = FindSolutionDirectory(AppContext.BaseDirectory) ??
                                FindSolutionDirectory(Directory.GetCurrentDirectory());
        if (solutionDirectory is not null)
            return Path.Combine(solutionDirectory, "data", "skills.db");

        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var databaseDirectory = string.IsNullOrWhiteSpace(localApplicationData)
            ? Path.Combine(AppContext.BaseDirectory, "data")
            : Path.Combine(localApplicationData, "SkillsAtlas");
        return Path.Combine(databaseDirectory, "skills.db");
    }

    private static string? FindSolutionDirectory(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SkillsAtlas.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }

        return null;
    }

    public async Task<RepositorySkills> FetchAsync(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("A repository URL is required.", nameof(source));

        using var checkout = await RepositoryCheckout.OpenAsync(source);
        var scannedSkills = SkillScanner.Scan(checkout);

        await _databaseLock.WaitAsync();
        try
        {
            using var database = new SkillsDatabase(_databasePath);
            database.Save(scannedSkills);
            var storedSkills = database.GetSkills(checkout.RepositoryUrl, checkout.CommitHash);
            return new RepositorySkills(
                checkout.RepositoryName,
                checkout.RepositoryUrl,
                checkout.CommitHash,
                storedSkills,
                _databasePath);
        }
        finally
        {
            _databaseLock.Release();
        }
    }

    public async Task<SkillsLibrary> GetStoredSkillsAsync()
    {
        await _databaseLock.WaitAsync();
        try
        {
            using var database = new SkillsDatabase(_databasePath);
            return new SkillsLibrary(database.GetAllSkills(), _databasePath);
        }
        finally
        {
            _databaseLock.Release();
        }
    }
}
