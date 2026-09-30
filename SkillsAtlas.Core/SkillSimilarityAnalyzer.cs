using System.Text.RegularExpressions;

namespace SkillsAtlas;

public sealed record SimilarSkill(SkillEntry Skill, double Similarity);

public sealed record SimilarSkillGroup(int Id, IReadOnlyList<SimilarSkill> Skills);

public sealed record SimilarSkillsResult(
    IReadOnlyList<SimilarSkillGroup> Groups,
    int AnalyzedSkillCount,
    string DatabasePath);

internal static partial class SkillSimilarityAnalyzer
{
    private const double SimilarityThreshold = 0.24;

    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "in", "is", "it", "of",
        "on", "or", "that", "the", "this", "to", "use", "using", "when", "with", "your"
    };

    public static IReadOnlyList<SimilarSkillGroup> FindGroups(IReadOnlyList<SkillEntry> skills)
    {
        if (skills.Count < 2)
            return [];

        var documents = skills.Select(skill => Tokenize(skill.ShortDescription)).ToArray();
        var documentFrequency = documents
            .SelectMany(document => document.Keys)
            .GroupBy(term => term, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var vectors = documents.Select(document => Vectorize(document, documentFrequency, skills.Count)).ToArray();
        var edges = new List<(int Left, int Right, double Score)>();

        for (var left = 0; left < skills.Count; left++)
        for (var right = left + 1; right < skills.Count; right++)
        {
            var score = CosineSimilarity(vectors[left], vectors[right]);
            if (score >= SimilarityThreshold)
                edges.Add((left, right, score));
        }

        var adjacency = Enumerable.Range(0, skills.Count).Select(_ => new List<(int Index, double Score)>()).ToArray();
        foreach (var edge in edges)
        {
            adjacency[edge.Left].Add((edge.Right, edge.Score));
            adjacency[edge.Right].Add((edge.Left, edge.Score));
        }

        var visited = new bool[skills.Count];
        var groups = new List<List<SimilarSkill>>();
        for (var start = 0; start < skills.Count; start++)
        {
            if (visited[start] || adjacency[start].Count == 0)
                continue;

            var members = new List<int>();
            var pending = new Stack<int>();
            pending.Push(start);
            visited[start] = true;
            while (pending.TryPop(out var current))
            {
                members.Add(current);
                foreach (var neighbor in adjacency[current])
                {
                    if (visited[neighbor.Index]) continue;
                    visited[neighbor.Index] = true;
                    pending.Push(neighbor.Index);
                }
            }

            groups.Add(members.Select(index => new SimilarSkill(
                    skills[index],
                    adjacency[index].Where(edge => members.Contains(edge.Index)).Max(edge => edge.Score)))
                .OrderByDescending(match => match.Similarity)
                .ThenBy(match => match.Skill.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        return groups
            .OrderByDescending(group => group.Max(match => match.Similarity))
            .ThenByDescending(group => group.Count)
            .Select((group, index) => new SimilarSkillGroup(index + 1, group))
            .ToList();
    }

    private static Dictionary<string, int> Tokenize(string text)
    {
        var words = WordRegex().Matches(text.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(word => word.Length > 2 && !StopWords.Contains(word))
            .Select(Stem)
            .ToList();
        var terms = words.Concat(words.Zip(words.Skip(1), (left, right) => $"{left}_{right}"));
        return terms.GroupBy(term => term, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    }

    private static string Stem(string word)
    {
        foreach (var suffix in new[] { "ing", "ed", "es", "s" })
            if (word.Length > suffix.Length + 3 && word.EndsWith(suffix, StringComparison.Ordinal))
                return word[..^suffix.Length];
        return word;
    }

    private static Dictionary<string, double> Vectorize(
        Dictionary<string, int> document,
        IReadOnlyDictionary<string, int> documentFrequency,
        int documentCount)
    {
        return document.ToDictionary(
            term => term.Key,
            term => (1 + Math.Log(term.Value)) * (Math.Log((documentCount + 1d) / (documentFrequency[term.Key] + 1d)) + 1d),
            StringComparer.Ordinal);
    }

    private static double CosineSimilarity(Dictionary<string, double> left, Dictionary<string, double> right)
    {
        if (left.Count == 0 || right.Count == 0)
            return 0;
        var dotProduct = left.Sum(term => term.Value * right.GetValueOrDefault(term.Key));
        var leftLength = Math.Sqrt(left.Values.Sum(value => value * value));
        var rightLength = Math.Sqrt(right.Values.Sum(value => value * value));
        return dotProduct / (leftLength * rightLength);
    }

    [GeneratedRegex("[\\p{L}\\p{N}]+")]
    private static partial Regex WordRegex();
}
