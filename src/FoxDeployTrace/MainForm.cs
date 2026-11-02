using System.Diagnostics;
using System.Text.Json;

namespace FoxDeployTrace;

internal sealed class MainForm : Form
{
    private readonly ListView _rows;
    private readonly TextBox _details;
    private readonly ToolStripStatusLabel _status;
    private readonly ToolStripStatusLabel _selection;
    private readonly ToolStripButton _hideIds;
    private readonly ToolStripButton _hideNullGuids;
    private readonly DisplaySettings _settings;
    private readonly string _settingsPath;
    private readonly Dictionary<int, ListViewItem> _itemCache = [];
    private readonly Font _logFont = new("Consolas", 10);
    private readonly Icon _appIcon = AppBranding.LoadIcon();
    private CancellationTokenSource? _loadCancellation;
    private LogDocument? _document;
    private string? _filePath;
    private string? _settingsWarning;

    public MainForm(string? initialPath = null, string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FDTrace", "settings.json");
        try
        {
            _settings = DisplaySettings.Load(_settingsPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _settings = new DisplaySettings();
            _settingsWarning = $"Could not load settings: {exception.Message} Defaults are in use.";
        }

        Text = "FDTrace";
        Icon = _appIcon;
        ClientSize = new Size(1300, 820);
        MinimumSize = new Size(760, 480);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;
        KeyPreview = true;

        var toolbar = new ToolStrip { Name = "Toolbar", GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6) };
        var open = new ToolStripButton("Open...") { ToolTipText = "Open a log (Ctrl+O)" };
        open.Click += async (_, _) => await OpenFileAsync();
        var reload = new ToolStripButton("Reload") { ToolTipText = "Reload the current file (F5)" };
        reload.Click += async (_, _) =>
        {
            if (_filePath is not null)
            {
                await LoadFileAsync(_filePath);
            }
        };
        var rules = new ToolStripButton("Highlight rules...");
        rules.Click += (_, _) => EditRules();
        _hideIds = new ToolStripButton("Hide PID / TID")
        {
            Name = "HideIds",
            CheckOnClick = true,
            Checked = _settings.HideProcessAndThreadIds
        };
        _hideNullGuids = new ToolStripButton("Hide null GUIDs")
        {
            Name = "HideNullGuids",
            CheckOnClick = true,
            Checked = _settings.HideNullGuids
        };
        _hideIds.CheckedChanged += (_, _) => ChangeDisplaySettings();
        _hideNullGuids.CheckedChanged += (_, _) => ChangeDisplaySettings();
        toolbar.Items.AddRange([open, reload, new ToolStripSeparator(), rules,
            new ToolStripSeparator(), _hideIds, _hideNullGuids]);
        var help = new ToolStripDropDownButton("&Help") { Name = "HelpMenu" };
        var about = new ToolStripMenuItem("&About FDTrace...") { Name = "AboutMenuItem" };
        about.Click += (_, _) => ShowAbout();
        help.DropDownItems.Add(about);
        toolbar.Items.Add(new ToolStripSeparator());
        toolbar.Items.Add(help);

        _status = new ToolStripStatusLabel("Open or drop a log file to begin.")
        {
            Name = "LoadStatus",
            Spring = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        _selection = new ToolStripStatusLabel { Name = "SelectionStatus" };
        var statusBar = new StatusStrip { Name = "StatusBar" };
        statusBar.Items.AddRange([_status, _selection]);

        _rows = new BufferedListView
        {
            Name = "LogRows",
            Dock = DockStyle.Fill,
            View = View.Details,
            VirtualMode = true,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = false,
            Font = _logFont,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.None
        };
        _rows.Columns.Add("Line", 75, HorizontalAlignment.Right);
        _rows.Columns.Add("Log text (select a row for full text below)", 4000);
        _rows.RetrieveVirtualItem += (_, e) => e.Item = GetItem(e.ItemIndex);
        _rows.SelectedIndexChanged += (_, _) => UpdateDetails();

        _details = new TextBox
        {
            Name = "RowDetails",
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            MaxLength = int.MaxValue,
            Font = _logFont,
            BackColor = SystemColors.Window,
            BorderStyle = BorderStyle.None
        };
        var detailsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        detailsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailsLayout.Controls.Add(new Label
        {
            Text = "Selected row - full text (read-only; Ctrl+C to copy)",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        }, 0, 0);
        detailsLayout.Controls.Add(_details, 0, 1);
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            Size = new Size(1300, 720),
            SplitterDistance = 510,
            Panel1MinSize = 100,
            Panel2MinSize = 100,
            SplitterWidth = 6
        };
        split.Panel1.Controls.Add(_rows);
        split.Panel2.Controls.Add(detailsLayout);
        Controls.Add(split);
        Controls.Add(toolbar);
        Controls.Add(statusBar);

        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += async (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
            {
                await LoadFileAsync(paths[0]);
            }
        };
        KeyDown += async (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.O)
            {
                e.SuppressKeyPress = true;
                await OpenFileAsync();
            }
            else if (e.KeyCode == Keys.F5 && _filePath is not null)
            {
                e.SuppressKeyPress = true;
                await LoadFileAsync(_filePath);
            }
            else if (e.KeyCode == Keys.F1)
            {
                e.SuppressKeyPress = true;
                ShowAbout();
            }
        };
        Shown += async (_, _) =>
        {
            if (initialPath is not null)
            {
                await LoadFileAsync(initialPath);
            }
            if (_settingsWarning is not null)
            {
                SetStatus(_settingsWarning, error: true);
            }
        };
    }

    private async Task OpenFileAsync()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Log files (*.log;*.txt;*.lo_)|*.log;*.txt;*.lo_|All files (*.*)|*.*",
            Title = "Open log file",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await LoadFileAsync(dialog.FileName);
        }
    }

    internal async Task LoadFileAsync(string path)
    {
        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        SetStatus($"Loading {path} ...");
        UseWaitCursor = true;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var document = await LogDocument.LoadAsync(path, cancellation.Token);
            if (cancellation.IsCancellationRequested || IsDisposed)
            {
                return;
            }

            _rows.BeginUpdate();
            try
            {
                _rows.SelectedIndices.Clear();
                _rows.VirtualListSize = 0;
                _itemCache.Clear();
                _document = document;
                _filePath = path;
                _rows.VirtualListSize = document.LineCount;
                _details.Clear();
                _selection.Text = "";
                if (document.LineCount > 0)
                {
                    _rows.SelectedIndices.Add(0);
                    _rows.EnsureVisible(0);
                }
                UpdateDetails();
            }
            finally
            {
                _rows.EndUpdate();
            }
            Text = $"FDTrace - {path}";
            SetStatus($"{document.LineCount:N0} rows | opened in {stopwatch.Elapsed.TotalMilliseconds:N0} ms | {path}");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer open request (or closing the window) supersedes this one.
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (!cancellation.IsCancellationRequested && !IsDisposed)
            {
                SetStatus($"Could not open {path}: {exception.Message}", error: true);
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
                if (!IsDisposed)
                {
                    UseWaitCursor = false;
                }
            }
        }
    }

    private ListViewItem GetItem(int index)
    {
        if (_itemCache.TryGetValue(index, out var item))
        {
            return item;
        }
        if (_document is null || index < 0 || index >= _document.LineCount)
        {
            return new ListViewItem(["", ""]);
        }
        var line = _document.GetLine(index);
        item = new ListViewItem([(index + 1).ToString(), LogDisplay.Format(line, _settings, preview: true)])
        {
            BackColor = _settings.MatchColor(line),
            ForeColor = SystemColors.WindowText,
            UseItemStyleForSubItems = true
        };
        // Keep only a small working set, independent of the number of log rows.
        if (_itemCache.Count >= 512)
        {
            _itemCache.Clear();
        }
        _itemCache[index] = item;
        return item;
    }

    private void UpdateDetails()
    {
        if (_document is null || _rows.SelectedIndices.Count == 0)
        {
            _details.Clear();
            _selection.Text = "";
            return;
        }
        var index = _rows.SelectedIndices[0];
        if (index >= _document.LineCount)
        {
            return;
        }
        _details.Text = LogDisplay.Format(_document.GetLine(index), _settings);
        _selection.Text = $"Line {index + 1:N0} / {_document.LineCount:N0}";
    }

    private void ChangeDisplaySettings()
    {
        _settings.HideProcessAndThreadIds = _hideIds.Checked;
        _settings.HideNullGuids = _hideNullGuids.Checked;
        RefreshRows();
        SaveSettings();
    }

    private void EditRules()
    {
        using var dialog = new RulesForm(_settings.Rules);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _settings.Rules = dialog.Rules;
            RefreshRows();
            SaveSettings();
        }
    }

    private void ShowAbout()
    {
        using var dialog = new AboutForm();
        dialog.ShowDialog(this);
    }

    private void RefreshRows()
    {
        _itemCache.Clear();
        _rows.Invalidate();
        UpdateDetails();
    }

    private void SaveSettings()
    {
        try
        {
            _settings.Save(_settingsPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Settings apply for this session but could not be saved: {exception.Message}", error: true);
        }
    }

    private void SetStatus(string text, bool error = false)
    {
        _status.Text = text;
        _status.ToolTipText = text;
        _status.ForeColor = error ? Color.Firebrick : SystemColors.ControlText;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _loadCancellation?.Cancel();
        }
        base.Dispose(disposing);
        if (disposing)
        {
            _logFont.Dispose();
            _appIcon.Dispose();
        }
    }

    private sealed class BufferedListView : ListView
    {
        public BufferedListView() => DoubleBuffered = true;
    }
}
