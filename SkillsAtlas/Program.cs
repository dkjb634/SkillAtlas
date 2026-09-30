using SkillsAtlas;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: SkillsAtlas <repository-url>");
    return 2;
}

try
{
    using var checkout = await RepositoryCheckout.OpenAsync(args[0]);
    var skills = SkillScanner.Scan(checkout);
    var databasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SkillsAtlas",
        "skills.db");

    using var database = new SkillsDatabase(databasePath);
    database.Save(skills);
    var storedSkills = database.GetSkills(checkout.RepositoryUrl, checkout.CommitHash);

    Console.WriteLine($"Found {skills.Count} skill(s) in {checkout.RepositoryName} ({checkout.CommitHash[..Math.Min(12, checkout.CommitHash.Length)]}).");
    Console.WriteLine($"SQLite database: {databasePath}");

    if (storedSkills.Count == 0)
    {
        Console.WriteLine("No SKILL.md files were found.");
        return 0;
    }

    SkillsTerminalUi.Run(checkout.RepositoryName, storedSkills);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Error: {exception.Message}");
    return 1;
}
