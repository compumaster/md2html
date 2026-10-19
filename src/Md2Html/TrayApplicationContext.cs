using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Md2Html;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;

    public TrayApplicationContext()
    {
        var menu = new ContextMenuStrip();
        var convert = new ToolStripMenuItem("Convert clipboard to HTML", null, (_, _) => Convert());
        convert.Font = new Font(convert.Font, FontStyle.Bold);
        var autoStart = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true, Checked = Settings.AutoStart };
        autoStart.CheckedChanged += (_, _) => Settings.AutoStart = autoStart.Checked;
        var notifications = new ToolStripMenuItem("Show notifications") { CheckOnClick = true, Checked = Settings.Notifications };
        notifications.CheckedChanged += (_, _) => Settings.Notifications = notifications.Checked;
        menu.Items.AddRange(
        [
            convert,
            new ToolStripSeparator(),
            autoStart,
            notifications,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()),
        ]);

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "md2html — click to convert clipboard text to HTML",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                Convert();
        };
    }

    private void Convert()
    {
        var outcome = ClipboardConverter.ConvertClipboard();
        if (Settings.Notifications || !outcome.Success)
            _notifyIcon.ShowBalloonTip(1500, "md2html", outcome.Message, outcome.Success ? ToolTipIcon.None : ToolTipIcon.Warning);
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        base.ExitThreadCore();
    }

    private static Icon CreateIcon()
    {
        const int size = 32;
        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var background = new SolidBrush(Color.FromArgb(0, 120, 212));
            using var shape = RoundedRectangle(new Rectangle(0, 0, size - 1, size - 1), 7);
            g.FillPath(background, shape);
            using var font = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("MD", font, Brushes.White, new RectangleF(0, 1, size, size), format);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle r, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.Left, r.Top, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
