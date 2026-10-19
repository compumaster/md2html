using System.Text;

namespace Md2Html.Core;

/// <summary>Builds the Windows "HTML Format" (CF_HTML) clipboard payload; offsets are UTF-8 byte positions.</summary>
public static class ClipboardHtml
{
    private const string HeaderTemplate =
        "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
    private const string Prefix = "<html><head><meta charset=\"utf-8\"></head><body>\r\n<!--StartFragment-->";
    private const string Suffix = "<!--EndFragment-->\r\n</body></html>";

    public static string Build(string fragmentHtml)
    {
        var utf8 = Encoding.UTF8;
        int startHtml = utf8.GetByteCount(string.Format(HeaderTemplate, 0, 0, 0, 0));
        int startFragment = startHtml + utf8.GetByteCount(Prefix);
        int endFragment = startFragment + utf8.GetByteCount(fragmentHtml);
        int endHtml = endFragment + utf8.GetByteCount(Suffix);
        return string.Format(HeaderTemplate, startHtml, endHtml, startFragment, endFragment)
            + Prefix + fragmentHtml + Suffix;
    }
}
