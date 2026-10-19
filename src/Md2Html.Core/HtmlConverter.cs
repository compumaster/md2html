using System.Text.RegularExpressions;
using Markdig;

namespace Md2Html.Core;

public sealed record ConversionResult(string Markdown, string HtmlFragment);

/// <summary>Normalizes CLI/Markdown text and renders it as paste-ready HTML with inline styles.</summary>
public static partial class HtmlConverter
{
    private const string BodyStyle = "font-family:Aptos,Calibri,'Segoe UI',Arial,sans-serif;font-size:11pt";

    private static readonly Dictionary<string, string> TagStyles = new()
    {
        ["table"] = "border-collapse:collapse;margin:6pt 0",
        ["th"] = "border:1px solid #999;padding:4px 8px;background:#f2f2f2;text-align:left;vertical-align:top",
        ["td"] = "border:1px solid #999;padding:4px 8px;vertical-align:top",
        ["pre"] = "font-family:Consolas,'Courier New',monospace;font-size:10pt;background:#f5f5f5;padding:6px",
        ["code"] = "font-family:Consolas,'Courier New',monospace;font-size:10pt",
        ["blockquote"] = "border-left:3px solid #ccc;margin-left:0;padding-left:10px;color:#555",
    };

    // Raw HTML is disabled so text like "<id>" in CLI output survives as literal text.
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAutoLinks()
        .UseEmphasisExtras()
        .UseTaskLists()
        .UseListExtras()
        .UseSoftlineBreakAsHardlineBreak()
        .DisableHtml()
        .Build();

    [GeneratedRegex(@"<(table|th|td|pre|code|blockquote)\b([^>]*)>")]
    private static partial Regex StyledTag();

    [GeneratedRegex(@"\bstyle=""([^""]*)""")]
    private static partial Regex StyleAttribute();

    public static ConversionResult Convert(string text)
    {
        string markdown = TextNormalizer.Normalize(text);
        string html = Markdown.ToHtml(markdown, Pipeline)
            .Replace(TextNormalizer.CellBreak.ToString(), "<br>");
        html = StyledTag().Replace(html, ApplyStyle);
        return new ConversionResult(
            markdown.Replace(TextNormalizer.CellBreak, ' '),
            $"<div style=\"{BodyStyle}\">{html}</div>");
    }

    private static string ApplyStyle(Match m)
    {
        string tag = m.Groups[1].Value;
        string attrs = m.Groups[2].Value;
        string style = TagStyles[tag];
        var existing = StyleAttribute().Match(attrs);
        if (existing.Success)
        {
            // Keep Markdown-provided styles (e.g. column alignment) last so they win.
            attrs = StyleAttribute().Replace(attrs, $"style=\"{style};{existing.Groups[1].Value}\"", 1);
            return $"<{tag}{attrs}>";
        }
        return $"<{tag} style=\"{style}\"{attrs}>";
    }
}
