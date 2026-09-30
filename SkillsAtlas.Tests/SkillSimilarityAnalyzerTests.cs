using SkillsAtlas;
using Xunit;

namespace SkillsAtlas.Tests;

public sealed class SkillSimilarityAnalyzerTests
{
    [Fact]
    public void FindGroups_GroupsSkillsWithSimilarDescriptions()
    {
        var skills = new[]
        {
            Create("review", "Review source code for security issues and coding quality."),
            Create("code-review", "Review code quality and find security problems in source code."),
            Create("image", "Generate artistic bitmap images from a creative prompt.")
        };

        var groups = SkillSimilarityAnalyzer.FindGroups(skills);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Skills.Count);
        Assert.Contains(group.Skills, match => match.Skill.Name == "review");
        Assert.Contains(group.Skills, match => match.Skill.Name == "code-review");
        Assert.All(group.Skills, match => Assert.InRange(match.Similarity, 0.24, 1));
    }

    [Fact]
    public void FindGroups_DoesNotGroupUnrelatedDescriptions()
    {
        var skills = new[]
        {
            Create("database", "Design relational database schemas and optimize SQL queries."),
            Create("illustration", "Paint watercolor landscapes and character illustrations.")
        };

        Assert.Empty(SkillSimilarityAnalyzer.FindGroups(skills));
    }

    private static SkillEntry Create(string name, string description) => new(
        "repository", "https://example.com/repository", name, description,
        $"{name}/SKILL.md", "commit", description, $"https://example.com/{name}");
}
