using System.ComponentModel;
using System.Diagnostics;

namespace FoxDeployTrace;

internal sealed class AboutForm : Form
{
    private readonly Bitmap _logo = AppBranding.LoadLogo();
    private readonly Icon _appIcon = AppBranding.LoadIcon();
    private readonly Font _headingFont = new(SystemFonts.MessageBoxFont!.FontFamily, 18, FontStyle.Bold);

    public AboutForm(Action<string>? openLink = null)
    {
        Text = "About FDTrace";
        Name = "AboutDialog";
        Icon = _appIcon;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(520, 610);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 9
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.Controls.Add(new PictureBox
        {
            Name = "AboutLogo",
            Image = _logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Fill,
            AccessibleName = "FDTrace fox and speedometer logo",
            TabStop = false
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Text = "FDTrace | FoxDeployTrace",
            Font = _headingFont,
            AutoSize = true,
            Anchor = AnchorStyles.None
        }, 0, 1);
        layout.Controls.Add(CenteredLabel("Ultra-speed, ultra-scale log file viewer."), 0, 2);
        var copyright = CenteredLabel(AppBranding.Copyright);
        copyright.Name = "AboutCopyright";
        layout.Controls.Add(copyright, 0, 3);
        layout.Controls.Add(CenteredLabel(
            "Ctrl+O: open a log | F5: reload | F1: about\r\n" +
            "Select a row for full text below; Ctrl+C to copy.\r\n" +
            "Highlight rules and ID-hiding toggles are on the toolbar."), 0, 4);
        var status = CenteredLabel("");
        status.Name = "AboutStatus";
        status.ForeColor = Color.Firebrick;
        status.AutoEllipsis = true;
        layout.Controls.Add(status, 0, 7);
        AddLink("BlogLink", "FoxDeploy blog", AppBranding.BlogUrl, 5);
        AddLink("ProjectLink", "FDTrace on GitHub", AppBranding.ProjectUrl, 6);
        var close = new Button
        {
            Name = "CloseAbout",
            Text = "Close",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            DialogResult = DialogResult.OK
        };
        layout.Controls.Add(close, 0, 8);
        AcceptButton = close;
        CancelButton = close;
        Controls.Add(layout);

        void AddLink(string name, string text, string url, int row)
        {
            var link = new LinkLabel
            {
                Name = name,
                Text = text,
                AutoSize = true,
                Anchor = AnchorStyles.None
            };
            link.Links.Add(0, text.Length, url);
            link.LinkClicked += (_, _) =>
            {
                try
                {
                    if (openLink is not null)
                    {
                        openLink(url);
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    status.Text = "";
                    link.LinkVisited = true;
                }
                catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
                {
                    status.Text = $"Could not open {url}: {exception.Message}";
                    status.AccessibleDescription = status.Text;
                }
            };
            layout.Controls.Add(link, 0, row);
        }
    }

    private static Label CenteredLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _logo.Dispose();
            _appIcon.Dispose();
            _headingFont.Dispose();
        }
    }
}
