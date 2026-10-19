using System.Text;

namespace Md2Html.Core;

/// <summary>Parses terminal box-drawing tables (Unicode or ASCII "+---+") into Markdown pipe tables.</summary>
internal static class BoxTable
{
    private const string Verticals = "│┃║|╎╏┆┇┊┋";
    private const string LeftEdges = "┌╭┏╔╒╓┍┎├┣╠╞╟┝┞┟┠┡┢└╰┗╚╘╙┕┖+";
    private const string RightEdges = "┐╮┓╗╕╖┑┒┤┫╣╡╢┥┦┧┨┩┪┘╯┛╝╛╜┙┚+";
    private const string BorderChars =
        "─━═┄┅┈┉╌╍-=:+ " +
        "┌┐└┘├┤┬┴┼╭╮╰╯┏┓┗┛┣┫┳┻╋╔╗╚╝╠╣╦╩╬╒╓╕╖╘╙╛╜╞╟╡╢╤╥╧╨╪╫" +
        "┍┎┑┒┕┖┙┚┝┞┟┠┡┢┥┦┧┨┩┪┭┮┯┰┱┲┵┶┷┸┹┺┽┾┿╀╁╂╃╄╅╆╇╈╉╊";

    private readonly record struct Fragment(string Text, int Width);

    /// <summary>
    /// Parses a table whose top border is at <paramref name="start"/>.
    /// Returns Markdown pipe-table lines and the number of source lines consumed.
    /// </summary>
    public static bool TryParse(IReadOnlyList<string> lines, int start, out List<string> markdown, out int consumed)
    {
        markdown = [];
        consumed = 0;
        if (start + 1 >= lines.Count || !IsBorder(lines[start].Trim()) || !IsData(lines[start + 1].Trim()))
            return false;

        int end = start + 1;
        while (end < lines.Count && (IsBorder(lines[end].Trim()) || IsData(lines[end].Trim())))
            end++;
        consumed = end - start;

        // Group data lines between separator borders.
        var groups = new List<List<Fragment[]>>();
        var current = new List<Fragment[]>();
        for (int i = start + 1; i < end; i++)
        {
            string t = lines[i].Trim();
            if (IsData(t))
            {
                current.Add(SplitCells(t));
            }
            else if (current.Count > 0)
            {
                groups.Add(current);
                current = [];
            }
        }
        if (current.Count > 0)
            groups.Add(current);

        // Every row boxed separately: a group is exactly one row. Otherwise (header-only or no separators)
        // physical lines must be split into rows by detecting which lines are wrapped continuations.
        var rows = groups.Count > 2 ? groups : groups.SelectMany(SplitRows).ToList();

        int columns = rows.Max(r => r.Max(l => l.Length));
        var cells = rows.Select(r => Enumerable.Range(0, columns).Select(c => JoinColumn(r, c)).ToArray()).ToList();

        markdown.Add(PipeRow(cells[0]));
        markdown.Add(PipeRow(Enumerable.Repeat("---", columns)));
        markdown.AddRange(cells.Skip(1).Select(PipeRow));
        return true;
    }

    private static IEnumerable<List<Fragment[]>> SplitRows(List<Fragment[]> group)
    {
        var row = new List<Fragment[]> { group[0] };
        for (int i = 1; i < group.Count; i++)
        {
            if (IsContinuation(group[i - 1], group[i]))
            {
                row.Add(group[i]);
            }
            else
            {
                yield return row;
                row = [group[i]];
            }
        }
        yield return row;
    }

    /// <summary>
    /// A line continues the previous row when its key (first) cell is empty, or when every non-empty cell is a
    /// soft wrap of the cell above.
    /// </summary>
    private static bool IsContinuation(Fragment[] above, Fragment[] line)
    {
        if (line.Length > 1 && line[0].Text.Length == 0 && line.Any(f => f.Text.Length > 0))
            return true;

        bool any = false;
        for (int c = 0; c < line.Length; c++)
        {
            if (line[c].Text.Length == 0)
                continue;
            any = true;
            if (c >= above.Length || above[c].Text.Length == 0
                || !TextNormalizer.IsCellSoftWrap(above[c].Text, line[c].Text, above[c].Width))
                return false;
        }
        return any;
    }

    private static string JoinColumn(List<Fragment[]> rowLines, int column)
    {
        var sb = new StringBuilder();
        Fragment? last = null;
        foreach (var line in rowLines)
        {
            if (column >= line.Length || line[column].Text.Length == 0)
                continue;
            var frag = line[column];
            if (last is { } prev)
            {
                if (TextNormalizer.IsCellSoftWrap(prev.Text, frag.Text, prev.Width))
                    sb.Append(TextNormalizer.Joiner(prev.Text, prev.Width));
                else
                    sb.Append(TextNormalizer.CellBreak);
            }
            sb.Append(frag.Text);
            last = frag;
        }
        return sb.ToString();
    }

    private static Fragment[] SplitCells(string dataLine)
    {
        var inner = dataLine[1..^1];
        var parts = inner.Split(Verticals.ToCharArray());
        // Cells are padded by one space on each side; the usable width excludes that padding.
        return parts.Select(p => new Fragment(p.Trim(), Math.Max(1, p.Length - 2))).ToArray();
    }

    private static string PipeRow(IEnumerable<string> cells) =>
        "| " + string.Join(" | ", cells.Select(c => c.Replace("|", "\\|"))) + " |";

    private static bool IsBorder(string t) =>
        t.Length >= 3
        && LeftEdges.Contains(t[0])
        && RightEdges.Contains(t[^1])
        && t.All(BorderChars.Contains);

    private static bool IsData(string t) =>
        t.Length >= 2 && Verticals.Contains(t[0]) && Verticals.Contains(t[^1]);
}
