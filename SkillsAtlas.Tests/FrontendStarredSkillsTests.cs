using Xunit;

namespace SkillsAtlas.Tests;

public sealed class FrontendStarredSkillsTests
{
    [Fact]
    public void SkillLibraryIncludesPersistentStarControlAndStarredWidget()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var app = File.ReadAllText(Path.Combine(root, "SkillsAtlas.Web/wwwroot/app.js"));
        var page = File.ReadAllText(Path.Combine(root, "SkillsAtlas.Web/wwwroot/index.html"));

        Assert.Contains("skills-atlas-starred", app);
        Assert.Contains("skillKey(skill)", app);
        Assert.Contains("star-skill", app);
        Assert.Contains("id=\"starred-widget\"", page);
        Assert.Contains("id=\"starred-list\"", page);
    }
}
