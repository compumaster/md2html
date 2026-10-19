using System.Text;
using Md2Html.Core;

namespace Md2Html.Tests;

public class ConverterTests
{
    // Terminal-style output: 1-space margin, hard-wrapped at 88 columns, box table with wrapped cells.
    private const string Sample = """
         I split the quarterly report into a short summary and a full appendix, and the original
         file is unchanged. The summary has 12 sections and takes about 10 minutes to read.

         Link: https://example.com/reports/q3-summary/
         Folder: Reports - Q3 summary/

         Agenda
         1. Opening, scope, and the list of open questions.
         2. Revenue: regional trends → pricing changes and discount policy → forecast for the
            next quarter and the risks behind it.
         3. Hiring update.

         What changed
         - I merged two or three sections of the full report into each summary section. Each
           summary section names the sections it came from, and no new numbers were added.
         - Removed:
           - The appendix charts.
           - The old roadmap.
           notes.md lists everything that was removed.

         Summary links

         ┌──────────────────────────┬──────────────────────────────────────────┐
         │ Document                 │ Link                                     │
         ├──────────────────────────┼──────────────────────────────────────────┤
         │ Q3 summary (new)         │ https://example.com/share/AbCdEfGhIjKlMn │
         │                          │ OpQrStUvWxYz0123456789/                  │
         ├──────────────────────────┼──────────────────────────────────────────┤
         │ Partner onboarding       │ https://example.com/share/onboarding/    │
         │ checklist and FAQ        │                                          │
         └──────────────────────────┴──────────────────────────────────────────┘
        """;

    private static string[] MarkdownLines(string input) => TextNormalizer.Normalize(input).Split('\n');

    [Fact]
    public void BoxTableRowsBecomePipeRowsWithWrappedCellsRejoined()
    {
        var lines = MarkdownLines(Sample);

        Assert.Contains("| Document | Link |", lines);
        // Hard-broken URL is re-joined without a space; word-wrapped text is re-joined with one.
        Assert.Contains("| Q3 summary (new) | https://example.com/share/AbCdEfGhIjKlMnOpQrStUvWxYz0123456789/ |", lines);
        Assert.Contains("| Partner onboarding checklist and FAQ | https://example.com/share/onboarding/ |", lines);
        Assert.Equal(4, lines.Count(l => l.StartsWith('|')));
    }

    [Fact]
    public void SoftWrappedParagraphsAndListItemsAreJoinedButRealBreaksKept()
    {
        var lines = MarkdownLines(Sample);

        Assert.Contains("I split the quarterly report into a short summary and a full appendix, and the original file is unchanged. The summary has 12 sections and takes about 10 minutes to read.", lines);
        Assert.Contains("2. Revenue: regional trends → pricing changes and discount policy → forecast for the next quarter and the risks behind it.", lines);
        Assert.Contains("- I merged two or three sections of the full report into each summary section. Each summary section names the sections it came from, and no new numbers were added.", lines);
        Assert.Contains("Link: https://example.com/reports/q3-summary/", lines);
        Assert.Contains("Folder: Reports - Q3 summary/", lines);
    }

    [Fact]
    public void RendersListsTablesAndSectionTitlesAsHtml()
    {
        string html = HtmlConverter.Convert(Sample).HtmlFragment;

        Assert.Contains("<strong>Agenda</strong>", html);
        Assert.Contains("<strong>Summary links</strong>", html);
        Assert.Contains("<li>Hiring update.</li>", html);
        Assert.Contains(">Partner onboarding checklist and FAQ</td>", html);
        // The trailing note belongs to the "Removed:" item, not to its last nested bullet.
        Assert.Contains("</ul>\n<p>notes.md lists everything that was removed.</p>", html);
        Assert.Contains("Link: <a href=\"https://example.com/reports/q3-summary/\">", html);
    }

    [Fact]
    public void ShortTextWithoutWrapEvidenceKeepsEveryLineBreak()
    {
        const string input = "Link: https://example.com/a/very/long/path/that/is/the/longest/line\nFolder: docs/\nOwner: me";

        Assert.Equal(input, TextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("├──────┼──────────────────────┤")] // every row boxed
    [InlineData(null)]                              // header-only separator
    public void IntentionalLineBreakInsideCellIsKept(string? rowSeparator)
    {
        var input = new List<string>
        {
            "┌──────┬──────────────────────┐",
            "│ Step │ Notes                │",
            "├──────┼──────────────────────┤",
            "│ 1    │ first line           │",
            "│      │ second               │",
        };
        if (rowSeparator != null)
            input.Add(rowSeparator);
        input.Add("│ 2    │ other                │");
        input.Add("└──────┴──────────────────────┘");

        string html = HtmlConverter.Convert(string.Join('\n', input)).HtmlFragment;

        Assert.Contains(">first line<br>second</td>", html);
        Assert.Contains(">other</td>", html);
    }

    [Fact]
    public void HeaderOnlySeparatorTableSplitsIndependentRows()
    {
        const string input = """
            +------+-------+
            | Name | Count |
            +------+-------+
            | foo  | 1     |
            | bar  | 2     |
            +------+-------+
            """;

        var lines = MarkdownLines(input);

        Assert.Equal(["| Name | Count |", "| --- | --- |", "| foo | 1 |", "| bar | 2 |"], lines);
    }

    [Fact]
    public void AngleBracketTextIsNotSwallowedAsHtml()
    {
        string html = HtmlConverter.Convert("Run deploy <env> --force").HtmlFragment;

        Assert.Contains("Run deploy &lt;env&gt; --force", html);
    }

    [Fact]
    public void ClipboardHtmlOffsetsAreUtf8ByteOffsets()
    {
        const string fragment = "<p>2–3 slides → done ✓</p>";
        byte[] payload = Encoding.UTF8.GetBytes(ClipboardHtml.Build(fragment));
        string header = Encoding.UTF8.GetString(payload, 0, 200);

        int Offset(string name)
        {
            int at = header.IndexOf(name + ":", StringComparison.Ordinal) + name.Length + 1;
            return int.Parse(header.AsSpan(at, 10));
        }

        int start = Offset("StartFragment");
        int end = Offset("EndFragment");
        Assert.Equal(fragment, Encoding.UTF8.GetString(payload, start, end - start));
        Assert.Equal(payload.Length, Offset("EndHTML"));
        Assert.StartsWith("<html>", Encoding.UTF8.GetString(payload, Offset("StartHTML"), 6));
    }
}
