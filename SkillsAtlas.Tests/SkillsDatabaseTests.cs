using SkillsAtlas;
using Xunit;

namespace SkillsAtlas.Tests;

public sealed class SkillsDatabaseTests
{
    [Fact]
    public void Save_DoesNotStoreSkillsWithTheSameNameAndContent()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"SkillsAtlas.Tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var databasePath = Path.Combine(temporaryDirectory, "skills.db");
            using var database = new SkillsDatabase(databasePath);

            var first = new SkillEntry(
                "repository-one", "https://example.com/one", "review", "First description",
                "skills/review/SKILL.md", "commit-one", "Review code carefully.", "https://example.com/one/review");
            var sameDefinitionFromAnotherRepository = first with
            {
                RepositoryName = "repository-two",
                RepositoryUrl = "https://example.com/two",
                RelativeFilePath = "agents/review/SKILL.md",
                CommitHash = "commit-two",
                FileUrl = "https://example.com/two/review"
            };
            var sameNameDifferentContent = first with
            {
                RepositoryName = "repository-three",
                RepositoryUrl = "https://example.com/three",
                CommitHash = "commit-three",
                Content = "Review code and tests carefully."
            };
            var differentNameSameContent = first with { Name = "code-review" };

            database.Save([first]);
            database.Save([sameDefinitionFromAnotherRepository, sameNameDifferentContent, differentNameSameContent]);

            var storedSkills = database.GetAllSkills();

            Assert.Equal(3, storedSkills.Count);
            Assert.Equal(
                storedSkills.Count,
                storedSkills.Select(skill => (skill.Name, skill.Content)).Distinct().Count());
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }
}
