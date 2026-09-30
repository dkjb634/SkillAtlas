using System.Diagnostics;
using System.Text;

namespace SkillsAtlas;

internal static class SkillsTerminalUi
{
    private const string ResetColor = "\u001b[0m";
    private const string Blue = "\u001b[34m";

    public static void Run(string repositoryName, IReadOnlyList<SkillEntry> skills)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            foreach (var (skill, index) in skills.Select((skill, index) => (skill, index)))
                Console.WriteLine($"[{index + 1}] {repositoryName}:{skill.Name}\n{skill.ShortDescription}\n{skill.FileUrl}");
            return;
        }

        var expandedIndex = -1;
        var scrollOffset = 0;
        var status = "Use ↑/↓ and Enter, click a number to expand, or click the file widget to open it. Press q to quit.";
        Console.Write("\u001b[?25l\u001b[?1000h\u001b[?1006h");

        try
        {
            while (true)
            {
                var document = BuildDocument(repositoryName, skills, expandedIndex, Math.Max(24, Console.WindowWidth - 1));
                var visibleLineCount = Math.Max(1, Console.WindowHeight - 3);
                scrollOffset = Math.Clamp(scrollOffset, 0, Math.Max(0, document.Lines.Count - visibleLineCount));
                Draw(repositoryName, document, expandedIndex, scrollOffset, visibleLineCount, status);

                var input = ReadInput();
                if (input.Kind == InputKind.Quit)
                    return;

                if (input.Kind == InputKind.Mouse)
                {
                    var lineIndex = input.Y - 3 + scrollOffset;
                    if (lineIndex >= 0 && lineIndex < document.Lines.Count)
                    {
                        var clickedLine = document.Lines[lineIndex];
                        if (clickedLine.Link is not null && clickedLine.Link.Contains(input.X))
                        {
                            try
                            {
                                OpenBrowser(clickedLine.Link.Url);
                                status = $"Opened {clickedLine.Link.Label} in your browser.";
                            }
                            catch (Exception exception)
                            {
                                status = $"Could not open browser: {exception.Message}";
                            }
                        }
                        else if (clickedLine.SkillIndex >= 0 && clickedLine.Toggle.Contains(input.X))
                        {
                            expandedIndex = expandedIndex == clickedLine.SkillIndex ? -1 : clickedLine.SkillIndex;
                            status = "Use ↑/↓ and Enter, click a number to expand, or click the file widget to open it. Press q to quit.";
                        }
                    }
                }
                else if (input.Kind == InputKind.Up)
                {
                    expandedIndex = Math.Max(0, expandedIndex == -1 ? 0 : expandedIndex - 1);
                    scrollOffset = ScrollToSkill(document.Lines, expandedIndex, scrollOffset, visibleLineCount);
                }
                else if (input.Kind == InputKind.Down)
                {
                    expandedIndex = Math.Min(skills.Count - 1, expandedIndex == -1 ? 0 : expandedIndex + 1);
                    scrollOffset = ScrollToSkill(document.Lines, expandedIndex, scrollOffset, visibleLineCount);
                }
                else if (input.Kind == InputKind.Toggle)
                {
                    var selected = expandedIndex < 0 ? 0 : expandedIndex;
                    expandedIndex = expandedIndex == selected ? -1 : selected;
                }
            }
        }
        finally
        {
            Console.Write("\u001b[?1006l\u001b[?1000l\u001b[0m\u001b[?25h");
            Console.Clear();
        }
    }

    private static int ScrollToSkill(IReadOnlyList<VisualLine> lines, int skillIndex, int currentOffset, int visibleLineCount)
    {
        var headerLine = -1;
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].SkillIndex == skillIndex && lines[index].Toggle is not null)
            {
                headerLine = index;
                break;
            }
        }

        if (headerLine < 0 || (headerLine >= currentOffset && headerLine < currentOffset + visibleLineCount))
            return currentOffset;
        return headerLine < currentOffset ? headerLine : headerLine - visibleLineCount + 1;
    }

    private static Document BuildDocument(string repositoryName, IReadOnlyList<SkillEntry> skills, int expandedIndex, int width)
    {
        var innerWidth = Math.Max(18, width - 2);
        var lines = new List<VisualLine>();
        for (var index = 0; index < skills.Count; index++)
        {
            var skill = skills[index];
            var selected = index == expandedIndex;
            lines.Add(FrameBorder(innerWidth, index, selected));

            var number = $"[{index + 1}]";
            var title = Sanitize($"{repositoryName}:{skill.Name}");
            var prefix = $" {number} {title}";
            var fileLabel = Sanitize(skill.RelativeFilePath);
            var maxWidgetLength = Math.Max(9, innerWidth / 2);
            if (fileLabel.Length + 6 > maxWidgetLength)
            {
                var suffixLength = Math.Max(1, maxWidgetLength - 7);
                fileLabel = "…" + fileLabel[^suffixLength..];
            }
            var widget = $" [{fileLabel} ↗] ";
            var widgetStart = Math.Max(1, innerWidth - widget.Length);
            prefix = Clip(prefix, Math.Max(0, widgetStart - 1));

            var header = new char[innerWidth];
            Array.Fill(header, ' ');
            prefix.AsSpan(0, Math.Min(prefix.Length, header.Length)).CopyTo(header);
            var safeWidget = Clip(widget, innerWidth - widgetStart);
            safeWidget.AsSpan().CopyTo(header.AsSpan(widgetStart));
            var numberOffset = Math.Max(0, prefix.IndexOf(number, StringComparison.Ordinal));
            lines.Add(new VisualLine(
                FrameLine(new string(header), innerWidth, selected), index,
                new HitRegion(numberOffset + 2, numberOffset + number.Length + 2),
                new LinkRegion(widgetStart + 2, widgetStart + safeWidget.Length + 2, skill.FileUrl, fileLabel)));

            foreach (var wrapped in Wrap(Sanitize(skill.ShortDescription), innerWidth - 2))
                        lines.Add(new VisualLine(FrameLine($" {wrapped}", innerWidth, selected), -1, HitRegion.Empty, null));

            if (selected)
            {
                lines.Add(FrameBorder(innerWidth, index, selected));
                foreach (var sourceLine in Sanitize(skill.Content).Split('\n'))
                {
                    var wrapped = Wrap(sourceLine, innerWidth - 2);
                    foreach (var text in wrapped)
                        lines.Add(new VisualLine(FrameLine($" {text}", innerWidth, selected), -1, HitRegion.Empty, null));
                }
            }

            lines.Add(FrameBorder(innerWidth, index, selected));
            lines.Add(new VisualLine(string.Empty, -1, HitRegion.Empty, null));
        }

        return new Document(lines);
    }

    private static VisualLine FrameBorder(int innerWidth, int skillIndex, bool selected)
    {
        var text = "+" + new string('-', innerWidth) + "+";
        return new VisualLine(selected ? Blue + text + ResetColor : text, skillIndex, HitRegion.Empty, null);
    }

    private static string FrameLine(string content, int innerWidth, bool selected)
    {
        var value = "|" + content.PadRight(innerWidth)[..innerWidth] + "|";
        return selected ? Blue + "|" + ResetColor + value[1..^1] + Blue + "|" + ResetColor : value;
    }

    private static void Draw(
        string repositoryName, Document document, int expandedIndex, int scrollOffset, int visibleLineCount, string status)
    {
        Console.SetCursorPosition(0, 0);
        Console.Write("\u001b[2J");
        Console.WriteLine($"Skills in {Sanitize(repositoryName)}  ({document.Lines.Count} display rows; open: {(expandedIndex < 0 ? "none" : $"[{expandedIndex + 1}]")})");
        Console.WriteLine(Clip(status, Math.Max(1, Console.WindowWidth - 1)));

        for (var visibleIndex = 0; visibleIndex < visibleLineCount; visibleIndex++)
        {
            var lineIndex = scrollOffset + visibleIndex;
            var text = lineIndex < document.Lines.Count ? document.Lines[lineIndex].Text : string.Empty;
            Console.Write(text);
            if (visibleIndex + 1 < visibleLineCount)
                Console.WriteLine();
        }
    }

    private static Input ReadInput()
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Q)
            return new Input(InputKind.Quit, 0, 0);
        if (key.Key is ConsoleKey.UpArrow or ConsoleKey.K)
            return new Input(InputKind.Up, 0, 0);
        if (key.Key is ConsoleKey.DownArrow or ConsoleKey.J)
            return new Input(InputKind.Down, 0, 0);
        if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
            return new Input(InputKind.Toggle, 0, 0);

        if (key.Key != ConsoleKey.Escape)
            return Input.None;

        var sequence = new StringBuilder();
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 100)
        {
            if (!Console.KeyAvailable)
            {
                Thread.Sleep(1);
                continue;
            }

            var next = Console.ReadKey(intercept: true);
            sequence.Append(next.KeyChar);
            if (sequence.Length > 40 || (sequence.Length > 0 && char.IsLetter(sequence[^1])))
                break;
        }

        var value = sequence.ToString();
        if (value.StartsWith("[<", StringComparison.Ordinal) && value.EndsWith('M'))
        {
            var coordinates = value[2..^1].Split(';');
            if (coordinates.Length == 3 && int.TryParse(coordinates[0], out var button) &&
                (button & 3) == 0 && int.TryParse(coordinates[1], out var x) && int.TryParse(coordinates[2], out var y))
                return new Input(InputKind.Mouse, x, y);
        }
        if (value.Length == 0)
            return new Input(InputKind.Quit, 0, 0);
        return Input.None;
    }

    private static void OpenBrowser(string url)
    {
        var executable = OperatingSystem.IsMacOS() ? "open" : OperatingSystem.IsWindows() ? "rundll32" : "xdg-open";
        var info = new ProcessStartInfo(executable) { UseShellExecute = false };
        if (OperatingSystem.IsWindows())
        {
            info.ArgumentList.Add("url.dll,FileProtocolHandler");
            info.ArgumentList.Add(url);
        }
        else
            info.ArgumentList.Add(url);

        _ = Process.Start(info) ?? throw new InvalidOperationException("The browser launcher could not be started.");
    }

    private static List<string> Wrap(string text, int width)
    {
        width = Math.Max(1, width);
        if (text.Length == 0)
            return new List<string> { string.Empty };

        var result = new List<string>();
        while (text.Length > width)
        {
            var split = text.LastIndexOf(' ', width - 1, width);
            if (split <= 0)
                split = width;
            result.Add(text[..split]);
            text = text[split..].TrimStart();
        }
        result.Add(text);
        return result;
    }

    private static string Clip(string text, int width) => text.Length <= width ? text : text[..Math.Max(0, width)];

    private static string Sanitize(string text)
    {
        var result = new StringBuilder(text.Length);
        foreach (var character in text)
            result.Append(char.IsControl(character) ? ' ' : character);
        return result.ToString();
    }

    private sealed record Document(List<VisualLine> Lines);
    private sealed record VisualLine(string Text, int SkillIndex, HitRegion Toggle, LinkRegion? Link);
    private sealed record HitRegion(int Start, int End)
    {
        public static HitRegion Empty { get; } = new(0, 0);
        public bool Contains(int x) => x >= Start && x < End;
    }
    private sealed record LinkRegion(int Start, int End, string Url, string Label)
    {
        public bool Contains(int x) => x >= Start && x < End;
    }
    private enum InputKind { None, Up, Down, Toggle, Mouse, Quit }
    private readonly record struct Input(InputKind Kind, int X, int Y)
    {
        public static Input None => new(InputKind.None, 0, 0);
    }
}
