using System.Text.RegularExpressions;

namespace Md2Html.Core;

/// <summary>
/// Turns terminal-rendered LLM/CLI output (hard-wrapped lines, box-drawing tables, indented margins)
/// back into clean Markdown whose remaining newlines are intentional.
/// </summary>
public static partial class TextNormalizer
{
    /// <summary>Marks an intentional line break inside a table cell; rendered as &lt;br&gt;.</summary>
    public const char CellBreak = '\uE000';

    // Minimum number of lines that look soft-wrapped before unwrapping is trusted.
    private const int MinWrapEvidence = 2;
    private const int MaxHeadingLength = 60;

    [GeneratedRegex(@"^( *)([-*+]|\d{1,9}[.)])( +|$)")]
    private static partial Regex ListMarker();

    [GeneratedRegex(@"^( *)[•◦▪‣∙·] +")]
    private static partial Regex UnicodeBullet();

    [GeneratedRegex(@"^ *(`{3,}|~{3,})")]
    private static partial Regex FenceOpen();

    [GeneratedRegex(@"^ *[─━═╌╍┄┅]{3,} *$")]
    private static partial Regex HorizontalRule();

    public static string Normalize(string input)
    {
        var lines = Prepare(input);
        if (lines.Count == 0)
            return string.Empty;

        int width = EstimateWrapWidth(lines);
        bool unwrap = CountWrapCandidates(lines, width) >= MinWrapEvidence;

        var output = new List<string>(lines.Count);
        string? prev = null;      // previous physical text line that a continuation may attach to
        int listContentCol = -1;  // content column of the innermost open list item
        string? fence = null;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];

            if (fence != null)
            {
                output.Add(line);
                if (line.TrimStart().StartsWith(fence, StringComparison.Ordinal))
                    fence = null;
                continue;
            }

            var fenceMatch = FenceOpen().Match(line);
            if (fenceMatch.Success)
            {
                fence = fenceMatch.Groups[1].Value;
                output.Add(line);
                prev = null;
                listContentCol = -1;
                continue;
            }

            if (BoxTable.TryParse(lines, i, out var tableRows, out int consumed))
            {
                AddBlank(output);
                output.AddRange(tableRows);
                output.Add(string.Empty);
                i += consumed - 1;
                prev = null;
                listContentCol = -1;
                continue;
            }

            if (line.Length == 0)
            {
                output.Add(string.Empty);
                prev = null;
                listContentCol = -1;
                continue;
            }

            if (HorizontalRule().IsMatch(line))
            {
                AddBlank(output);
                output.Add("---");
                output.Add(string.Empty);
                prev = null;
                listContentCol = -1;
                continue;
            }

            line = UnicodeBullet().Replace(line, "$1- ");
            int indent = Indent(line);
            bool isItem = TryListItem(line, out int itemContentCol);

            if (unwrap && prev != null && !isItem && !StartsBlock(line) && IsSoftWrap(prev, line, width))
            {
                output[^1] += Joiner(prev, width) + line.TrimStart();
                prev = line;
                continue;
            }

            // A non-item line outdented from the current item belongs to an outer block; a blank line
            // stops Markdown from lazily appending it to the inner item's paragraph.
            if (!isItem && listContentCol >= 0 && indent < listContentCol && prev != null)
            {
                output.Add(string.Empty);
                listContentCol = -1;
            }

            output.Add(line);
            prev = line;
            if (isItem)
                listContentCol = itemContentCol;
        }

        EmphasizeHeadings(output);
        TrimBlankRuns(output);
        return string.Join('\n', output).Trim('\n');
    }

    private static List<string> Prepare(string input)
    {
        string text = input
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Replace("\t", "    ")
            .Replace('\u00A0', ' ');

        var lines = text.Split('\n').Select(l => l.TrimEnd()).ToList();
        while (lines.Count > 0 && lines[0].Length == 0)
            lines.RemoveAt(0);
        while (lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);
        if (lines.Count == 0)
            return lines;

        // Claude Code prefixes a response with "● " / "⏺ "; turn the marker into margin.
        string first = lines[0];
        int firstIndent = Indent(first);
        if (first[firstIndent] is '●' or '⏺' && (first.Length == firstIndent + 1 || first[firstIndent + 1] == ' '))
            lines[0] = new string(' ', firstIndent + 1) + first[(firstIndent + 1)..];

        int common = lines.Where(l => l.Length > 0).Min(Indent);
        if (common > 0)
            lines = lines.Select(l => l.Length == 0 ? l : l[common..]).ToList();
        return lines;
    }

    /// <summary>The terminal wrap width is best approximated by the longest non-table line.</summary>
    private static int EstimateWrapWidth(List<string> lines)
    {
        int width = 0;
        foreach (var line in TextLines(lines))
            width = Math.Max(width, line.Length);
        return width;
    }

    private static int CountWrapCandidates(List<string> lines, int width)
    {
        int count = 0;
        string? prev = null;
        foreach (var line in TextLines(lines, yieldBreaks: true))
        {
            if (line.Length == 0)
            {
                prev = null;
                continue;
            }
            if (prev != null && !TryListItem(line, out _) && !StartsBlock(line) && IsSoftWrap(prev, line, width))
                count++;
            prev = line;
        }
        return count;
    }

    /// <summary>Lines outside tables and code fences; tables/fences surface as empty breaks when requested.</summary>
    private static IEnumerable<string> TextLines(List<string> lines, bool yieldBreaks = false)
    {
        string? fence = null;
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (fence != null)
            {
                if (line.TrimStart().StartsWith(fence, StringComparison.Ordinal))
                    fence = null;
                continue;
            }
            var fenceMatch = FenceOpen().Match(line);
            if (fenceMatch.Success)
            {
                fence = fenceMatch.Groups[1].Value;
                if (yieldBreaks)
                    yield return string.Empty;
                continue;
            }
            if (BoxTable.TryParse(lines, i, out _, out int consumed))
            {
                i += consumed - 1;
                if (yieldBreaks)
                    yield return string.Empty;
                continue;
            }
            yield return line;
        }
    }

    /// <summary>
    /// True when <paramref name="next"/> is the continuation of a terminal-wrapped <paramref name="prev"/>:
    /// its first word would not have fit on the previous line and its indent lines up with the previous text.
    /// </summary>
    private static bool IsSoftWrap(string prev, string next, int width)
    {
        int prevIndent = Indent(prev);
        int prevContentCol = TryListItem(prev, out int col) ? col : prevIndent;
        int nextIndent = Indent(next);
        if (nextIndent < prevIndent || nextIndent > prevContentCol)
            return false;
        return prev.Length + 1 + FirstWord(next.TrimStart()).Length > width;
    }

    /// <summary>Words longer than the line are hard-broken by the terminal and must be re-joined without a space.</summary>
    internal static string Joiner(string prev, int width)
    {
        if (prev.Length < width)
            return " ";
        string last = LastWord(prev);
        return last.Contains("://", StringComparison.Ordinal) || last.Length >= Math.Min(20, width) ? string.Empty : " ";
    }

    internal static bool IsCellSoftWrap(string prev, string next, int width) =>
        prev.Length + 1 + FirstWord(next).Length > width;

    private static bool TryListItem(string line, out int contentCol)
    {
        var m = ListMarker().Match(line);
        if (!m.Success)
        {
            contentCol = -1;
            return false;
        }
        contentCol = m.Groups[1].Length + m.Groups[2].Length + Math.Max(1, m.Groups[3].Length);
        return true;
    }

    private static bool StartsBlock(string line)
    {
        string t = line.TrimStart();
        return t.StartsWith('#') || t.StartsWith('>') || t.StartsWith('|');
    }

    /// <summary>
    /// CLI renderers drop Markdown heading/bold markup. A short, unpunctuated standalone line that introduces
    /// a list or table is almost always a section title, so it is restored as bold text.
    /// </summary>
    private static void EmphasizeHeadings(List<string> lines)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            if (!IsHeadingCandidate(line) || (i > 0 && lines[i - 1].Length != 0))
                continue;

            int next = i + 1;
            while (next < lines.Count && lines[next].Length == 0)
                next++;
            if (next >= lines.Count)
                continue;

            string following = lines[next];
            bool introducesList = TryListItem(following, out _) && Indent(following) == 0;
            bool introducesTable = following.StartsWith('|');
            if (introducesList || introducesTable)
                lines[i] = "**" + line + "**";
        }
    }

    private static bool IsHeadingCandidate(string line) =>
        line.Length is > 0 and <= MaxHeadingLength
        && Indent(line) == 0
        && char.IsLetterOrDigit(line[0])
        && !TryListItem(line, out _)
        && !".:;,!?".Contains(line[^1])
        && !line.Contains("**", StringComparison.Ordinal);

    private static void AddBlank(List<string> output)
    {
        if (output.Count > 0 && output[^1].Length != 0)
            output.Add(string.Empty);
    }

    private static void TrimBlankRuns(List<string> lines)
    {
        for (int i = lines.Count - 1; i > 0; i--)
        {
            if (lines[i].Length == 0 && lines[i - 1].Length == 0)
                lines.RemoveAt(i);
        }
    }

    internal static int Indent(string line)
    {
        int i = 0;
        while (i < line.Length && line[i] == ' ')
            i++;
        return i;
    }

    private static string FirstWord(string text)
    {
        int space = text.IndexOf(' ');
        return space < 0 ? text : text[..space];
    }

    private static string LastWord(string text)
    {
        int space = text.LastIndexOf(' ');
        return space < 0 ? text : text[(space + 1)..];
    }
}
