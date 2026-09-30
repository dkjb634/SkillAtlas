using Ganss.Xss;
using Markdig;
using SkillsAtlas;

internal sealed record SkillMarkdownView(
    string RepositoryName,
    string RepositoryUrl,
    string Name,
    string ShortDescription,
    string RelativeFilePath,
    string CommitHash,
    string FileUrl,
    string ContentHtml);

internal static class SkillMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static SkillMarkdownView Map(SkillEntry skill) => new(
        skill.RepositoryName,
        skill.RepositoryUrl,
        skill.Name,
        skill.ShortDescription,
        skill.RelativeFilePath,
        skill.CommitHash,
        skill.FileUrl,
        Sanitize(Markdown.ToHtml(skill.Content, Pipeline)));

    private static string Sanitize(string html)
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith([
            "a", "abbr", "b", "blockquote", "br", "caption", "code", "col", "colgroup", "dd", "del", "div",
            "dl", "dt", "em", "h1", "h2", "h3", "h4", "h5", "h6", "hr", "i", "img", "input", "ins",
            "kbd", "li", "ol", "p", "pre", "s", "span", "strong", "sub", "sup", "table", "tbody", "td",
            "th", "thead", "tr", "u", "ul"
        ]);

        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith([
            "alt", "checked", "class", "disabled", "href", "id", "src", "start", "title", "type"
        ]);

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https", "mailto"]);
        sanitizer.AllowedCssProperties.Clear();

        return sanitizer.Sanitize(html);
    }
}
