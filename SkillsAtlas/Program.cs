using SkillsAtlas;

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Usage: SkillsAtlas <repository-url>");
    return 2;
}

try
{
    var result = await new SkillsCatalog().FetchAsync(args[0]);

    Console.WriteLine($"Found {result.Skills.Count} skill(s) in {result.RepositoryName} ({result.CommitHash[..Math.Min(12, result.CommitHash.Length)]}).");
    Console.WriteLine($"SQLite database: {result.DatabasePath}");

    if (result.Skills.Count == 0)
    {
        Console.WriteLine("No SKILL.md files were found.");
        return 0;
    }

    SkillsTerminalUi.Run(result.RepositoryName, result.Skills);
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Error: {exception.Message}");
    return 1;
}
