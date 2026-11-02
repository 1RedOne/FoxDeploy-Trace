using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FastLogViewer;

namespace FastLogViewer.Tests;

internal static class Program
{
    private static readonly string TestDirectory = Path.Combine(Path.GetTempPath(), $"FastLogViewer-tests-{Guid.NewGuid():N}");
    private static int _passed;

    [STAThread]
    private static int Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var result = 1;
        using var host = new Form { ShowInTaskbar = false, WindowState = FormWindowState.Minimized };
        host.Shown += (_, _) => host.BeginInvoke((Action)(() =>
        {
            result = RunTests();
            host.Close();
        }));
        Application.Run(host);
        return result;
    }

    private static int RunTests()
    {
        Directory.CreateDirectory(TestDirectory);
        try
        {
            Run("Line indexing preserves empty rows and all line endings", TestLineIndex);
            Run("Display toggles remove only legacy IDs and zero GUIDs", TestDisplay);
            Run("Multiple highlighting rules, priority and disabled rules", TestHighlighting);
            Run("Settings persistence and invalid settings errors", TestSettings);
            Run("File encodings, cancellation and open-for-writing logs", TestFileLoading);
            Run("Rules dialog add, remove, reorder and apply", TestRulesDialog);
            Run("10 MB real UI load, selection, toggles, rules and reload", TestLargeFileUi);
            Run("Very long row has bounded preview and complete details", TestLongRowUi);
            Console.WriteLine($"PASS: {_passed} test groups.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            foreach (var file in Directory.EnumerateFiles(TestDirectory))
            {
                File.Delete(file);
            }
            Directory.Delete(TestDirectory);
        }
    }

    private static void Run(string name, Action test)
    {
        test();
        _passed++;
        Console.WriteLine($"PASS: {name}");
    }

    private static void TestLineIndex()
    {
        foreach (var (text, expected) in new (string, string[])[]
        {
            ("", []),
            ("one", ["one"]),
            ("one\r\n", ["one"]),
            ("\n", [""]),
            ("\r\n\r\n", ["", ""]),
            ("a\r\n\r\nb\nc\rd", ["a", "", "b", "c", "d"]),
            ("a\n\n", ["a", ""]),
            ("a\rb\r", ["a", "b"])
        })
        {
            var document = LogDocument.Parse(text);
            Equal(expected.Length, document.LineCount);
            for (var i = 0; i < expected.Length; i++)
            {
                Equal(expected[i], document.GetLine(i).ToString());
            }
        }
        Throws<ArgumentOutOfRangeException>(() => LogDocument.Parse("a").GetLine(1).ToString());
    }

    private static void TestDisplay()
    {
        const string raw = "[time] [PID 2443][TID 2786] [INFO] [00000000-0000-0000-0000-000000000000] [00000-0000] [145bfb3b-2291-4d4d-b76f-eb3a70d81fb9] Recieved";
        var settings = new DisplaySettings();
        Equal(raw, LogDisplay.Format(raw, settings));
        settings.HideProcessAndThreadIds = true;
        Equal("[time] [INFO] [00000000-0000-0000-0000-000000000000] [00000-0000] [145bfb3b-2291-4d4d-b76f-eb3a70d81fb9] Recieved",
            LogDisplay.Format(raw, settings));
        settings.HideNullGuids = true;
        Equal("[time] [INFO] [145bfb3b-2291-4d4d-b76f-eb3a70d81fb9] Recieved", LogDisplay.Format(raw, settings));
        settings.HideProcessAndThreadIds = false;
        Assert(LogDisplay.Format(raw, settings).Contains("[PID 2443][TID 2786]"), "Toggles must be independent.");
        Equal("[PID abc] [TID -1] [00000] [0000-0001]", LogDisplay.Format("[PID abc] [TID -1] [00000] [0000-0001]", settings));
        settings.HideProcessAndThreadIds = true;
        Equal("message", LogDisplay.Format("[pid 1]\t[tid 2] message", settings));
        Equal(raw, LogDocument.Parse(raw).GetLine(0).ToString());
    }

    private static void TestHighlighting()
    {
        var settings = new DisplaySettings();
        Equal(DisplaySettings.GetColor("Cyan"), settings.MatchColor("Recieved assignment"));
        Equal(DisplaySettings.GetColor("Cyan"), settings.MatchColor("RECEIVED assignment"));
        Equal(DisplaySettings.GetColor("Orange"), settings.MatchColor("Set configuration"));
        Equal(DisplaySettings.GetColor("Cyan"), settings.MatchColor("Set Recieved"));
        settings.Rules[0] = settings.Rules[0] with { Enabled = false };
        Equal(DisplaySettings.GetColor("Orange"), settings.MatchColor("Set Recieved"));
        Equal(SystemColors.Window, settings.MatchColor("no matching rule"));
        settings.Rules = [];
        Equal(SystemColors.Window, settings.MatchColor("Set"));
    }

    private static void TestSettings()
    {
        var path = Path.Combine(TestDirectory, "settings.json");
        Equal(3, DisplaySettings.Load(path).Rules.Count);
        var settings = new DisplaySettings
        {
            HideNullGuids = true,
            HideProcessAndThreadIds = true,
            Rules = [new("something", "Pink", false), new("else", "Orange")]
        };
        settings.Save(path);
        var restored = DisplaySettings.Load(path);
        Assert(restored.HideNullGuids && restored.HideProcessAndThreadIds, "Both toggles must persist.");
        Assert(settings.Rules.SequenceEqual(restored.Rules), "Rules must round-trip in priority order.");
        settings.Rules = [];
        settings.Save(path);
        Equal(0, DisplaySettings.Load(path).Rules.Count);
        foreach (var json in new[]
        {
            "null", "{broken", "{\"Rules\":null}", "{\"Rules\":[null]}",
            "{\"Rules\":[{\"Text\":\"x\",\"ColorName\":\"invalid\"}]}",
            "{\"Rules\":[{\"Text\":\" \",\"ColorName\":\"Cyan\"}]}"
        })
        {
            File.WriteAllText(path, json);
            Throws<JsonException>(() => DisplaySettings.Load(path));
        }
        File.Delete(path);
    }

    private static void TestFileLoading()
    {
        const string content = "[INFO] Received caf\u00e9 \u4e2d\u6587\r\nsecond row";
        foreach (var encoding in new Encoding[] { new UTF8Encoding(false), Encoding.Unicode, Encoding.BigEndianUnicode, Encoding.UTF32 })
        {
            var path = Path.Combine(TestDirectory, "encoding.log");
            File.WriteAllText(path, content, encoding);
            using var writer = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            var document = Pump(LogDocument.LoadAsync(path));
            Equal(2, document.LineCount);
            Equal(content.Split("\r\n")[0], document.GetLine(0).ToString());
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Throws<OperationCanceledException>(() => LogDocument.Parse("a", cancellation.Token));
        Throws<OperationCanceledException>(() => Pump(LogDocument.LoadAsync("missing", cancellation.Token)));
        Throws<FileNotFoundException>(() => Pump(LogDocument.LoadAsync(Path.Combine(TestDirectory, "missing.log"))));
    }

    private static void TestRulesDialog()
    {
        using var form = new RulesForm([new("first", "Cyan"), new("second", "Orange")]);
        form.Show();
        Application.DoEvents();
        var grid = Find<DataGridView>(form, "RulesGrid");
        grid.CurrentCell = grid.Rows[1].Cells[1];
        ClickButton(form, "Up");
        Equal("second", grid.Rows[0].Cells[1].Value);
        ClickButton(form, "Down");
        Equal("first", grid.Rows[0].Cells[1].Value);
        ClickButton(form, "Add");
        grid.EndEdit();
        grid.CurrentRow!.Cells[1].Value = "third";
        grid.CurrentRow.Cells[2].Value = "Pink";
        grid.CurrentRow.Cells[0].Value = false;
        ClickButton(form, "Apply");
        Equal(3, form.Rules.Count);
        Equal(new HighlightRule("third", "Pink", false), form.Rules[2]);
        ClickButton(form, "Remove");
        ClickButton(form, "Apply");
        Equal(2, form.Rules.Count);
        form.Close();
    }

    private static void TestLargeFileUi()
    {
        var path = Path.Combine(TestDirectory, "10MB.log");
        const string prefix = "[2026-10-29 20:43:07.109] [PID 2443] [TID 2786] [Timer Manager] [INFO] [00000000-0000-0000-0000-000000000000] ";
        var received = prefix + "Recieved assignment " + new string('x', 120);
        var set = prefix + "Set configuration " + new string('y', 120);
        var expectedCount = 0;
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            while (expectedCount * (received.Length + set.Length + 4L) / 2 < 10 * 1024 * 1024)
            {
                writer.Write(expectedCount % 2 == 0 ? received : set);
                writer.Write("\r\n");
                expectedCount++;
            }
        }
        var settingsPath = Path.Combine(TestDirectory, "ui-settings.json");
        using var form = new MainForm(settingsPath: settingsPath);
        form.Show();
        Application.DoEvents();
        var rows = Find<ListView>(form, "LogRows");
        var details = Find<TextBox>(form, "RowDetails");
        var toolbar = Find<ToolStrip>(form, "Toolbar");
        var status = Find<StatusStrip>(form, "StatusBar");
        Assert(rows.VirtualMode, "UI must use virtual rows.");
        Assert(details.ReadOnly, "Details must be read-only.");
        var before = GC.GetTotalMemory(true);
        var stopwatch = Stopwatch.StartNew();
        var load = form.LoadFileAsync(path);
        Assert(toolbar.Enabled, "Opening logs must not disable the toolbar.");
        Pump(load);
        stopwatch.Stop();
        Application.DoEvents();
        var retainedBytes = GC.GetTotalMemory(true) - before;
        var fileBytes = new FileInfo(path).Length;
        Equal(expectedCount, rows.VirtualListSize);
        Equal(received, details.Text);
        Equal(DisplaySettings.GetColor("Cyan"), rows.Items[0].BackColor);
        Equal(DisplaySettings.GetColor("Orange"), rows.Items[1].BackColor);
        Assert(stopwatch.Elapsed < TimeSpan.FromSeconds(5), "10 MB UI open must finish within 5 seconds.");
        Assert(retainedBytes < 80 * 1024 * 1024, "10 MB log must retain less than 80 MB managed memory.");
        Console.WriteLine($"BENCHMARK: {fileBytes / 1048576.0:F2} MiB; {expectedCount:N0} rows; UI open {stopwatch.Elapsed.TotalMilliseconds:F0} ms; retained managed memory {retainedBytes / 1048576.0:F2} MiB.");

        rows.SelectedIndices.Clear();
        rows.SelectedIndices.Add(1);
        Application.DoEvents();
        Equal(set, details.Text);
        ((ToolStripButton)toolbar.Items["HideIds"]!).PerformClick();
        Assert(!details.Text.Contains("[PID") && !details.Text.Contains("[TID"), "Selected detail must hide IDs.");
        Assert(details.Text.Contains("[00000000-0000"), "GUID toggle must remain independent.");
        ((ToolStripButton)toolbar.Items["HideNullGuids"]!).PerformClick();
        Assert(!details.Text.Contains("[00000000-0000"), "Selected detail must hide null GUIDs.");
        Assert(!rows.Items[1].SubItems[1].Text.Contains("[PID"), "Visible rows must refresh with toggles.");
        ((ToolStripButton)toolbar.Items["HideIds"]!).PerformClick();
        ((ToolStripButton)toolbar.Items["HideNullGuids"]!).PerformClick();
        Equal(set, details.Text);

        using var editRules = new System.Windows.Forms.Timer { Interval = 100 };
        editRules.Tick += (_, _) =>
        {
            var rulesForm = Application.OpenForms.OfType<RulesForm>().SingleOrDefault();
            if (rulesForm is null || !rulesForm.Visible)
            {
                return;
            }
            editRules.Stop();
            var grid = Find<DataGridView>(rulesForm, "RulesGrid");
            grid.Rows.Clear();
            grid.Rows.Add(true, "Set", "Pink");
            grid.Rows.Add(true, "Recieved", "Green");
            ClickButton(rulesForm, "Apply");
        };
        editRules.Start();
        toolbar.Items.Cast<ToolStripItem>().Single(item => item.Text == "Highlight rules...").PerformClick();
        Equal(DisplaySettings.GetColor("Green"), rows.Items[0].BackColor);
        Equal(DisplaySettings.GetColor("Pink"), rows.Items[1].BackColor);
        Equal("Pink", DisplaySettings.Load(settingsPath).Rules[0].ColorName);
        rows.SelectedIndices.Clear();
        rows.SelectedIndices.Add(expectedCount - 1);
        rows.EnsureVisible(expectedCount - 1);
        Application.DoEvents();
        Equal(expectedCount % 2 == 0 ? set : received, details.Text);

        Pump(form.LoadFileAsync(Path.Combine(TestDirectory, "missing-ui.log")));
        Equal(expectedCount, rows.VirtualListSize);
        Assert(status.Items["LoadStatus"]!.Text!.StartsWith("Could not open"), "Open failure must be visible.");
        File.WriteAllText(path, "replacement\r\n");
        toolbar.Items.Cast<ToolStripItem>().Single(item => item.Text == "Reload").PerformClick();
        PumpUntil(() => rows.VirtualListSize == 1);
        Equal("replacement", details.Text);

        var empty = Path.Combine(TestDirectory, "empty.log");
        File.WriteAllText(empty, "");
        var superseded = form.LoadFileAsync(path);
        var newest = form.LoadFileAsync(empty);
        Pump(Task.WhenAll(superseded, newest));
        Equal(0, rows.VirtualListSize);
        Equal("", details.Text);
        form.Close();
    }

    private static void TestLongRowUi()
    {
        var path = Path.Combine(TestDirectory, "long.log");
        var text = new string('a', 100_000) + "Set after the preview";
        File.WriteAllText(path, text);
        using var form = new MainForm(settingsPath: Path.Combine(TestDirectory, "long-settings.json"));
        form.Show();
        Pump(form.LoadFileAsync(path));
        var rows = Find<ListView>(form, "LogRows");
        Assert(rows.Items[0].SubItems[1].Text.Length < 4200, "Preview allocation must be bounded.");
        Equal(DisplaySettings.GetColor("Orange"), rows.Items[0].BackColor);
        Equal(text, Find<TextBox>(form, "RowDetails").Text);
        form.Close();
    }

    private static T Find<T>(Control parent, string name) where T : Control =>
        (T)parent.Controls.Find(name, searchAllChildren: true).Single();

    private static void ClickButton(Control parent, string text)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is Button button && button.Text == text)
            {
                button.PerformClick();
                return;
            }
            if (control.HasChildren)
            {
                var match = Descendants(control).OfType<Button>().FirstOrDefault(candidate => candidate.Text == text);
                if (match is not null)
                {
                    match.PerformClick();
                    return;
                }
            }
        }
        throw new InvalidOperationException($"Button not found: {text}");
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static T Pump<T>(Task<T> task)
    {
        PumpUntil(() => task.IsCompleted);
        return task.GetAwaiter().GetResult();
    }

    private static void Pump(Task task)
    {
        PumpUntil(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition())
        {
            Assert(timeout.Elapsed < TimeSpan.FromSeconds(15), "Timed out waiting for UI operation.");
            Application.DoEvents();
            Thread.Sleep(1);
        }
        Application.DoEvents();
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Expected <{expected}>, got <{actual}>.");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
