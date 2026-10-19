using System.Runtime.InteropServices;
using Md2Html.Core;

namespace Md2Html;

internal readonly record struct ConversionOutcome(bool Success, string Message);

internal static class ClipboardConverter
{
    // Text we last placed on the clipboard; re-clicking must not re-normalize already converted output.
    private static string? s_lastOutput;

    public static ConversionOutcome ConvertClipboard()
    {
        string text;
        try
        {
            text = Clipboard.GetText(TextDataFormat.UnicodeText);
        }
        catch (ExternalException ex)
        {
            return new(false, $"Clipboard is busy: {ex.Message}");
        }

        if (string.IsNullOrWhiteSpace(text))
            return new(false, "Clipboard has no text to convert.");
        if (text == s_lastOutput)
            return new(true, "Already converted — paste with Ctrl+V.");

        var result = HtmlConverter.Convert(text);
        string plainText = result.Markdown.ReplaceLineEndings("\r\n");
        var data = new DataObject();
        data.SetData(DataFormats.Html, ClipboardHtml.Build(result.HtmlFragment));
        data.SetData(DataFormats.UnicodeText, plainText);
        try
        {
            Clipboard.SetDataObject(data, copy: true, retryTimes: 10, retryDelay: 100);
        }
        catch (ExternalException ex)
        {
            return new(false, $"Could not write clipboard: {ex.Message}");
        }

        s_lastOutput = plainText;
        return new(true, "Converted to HTML — paste with Ctrl+V.");
    }
}
