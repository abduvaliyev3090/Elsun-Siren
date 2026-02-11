using Microsoft.Win32;
using System.Media;

namespace SirenScheduler;

public sealed class MainForm : Form
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "ElsunSiren";

    private readonly AppSettings _settings;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _tickTimer;

    private readonly CheckBox _enabled = new() { Text = "Enable scheduler", AutoSize = true };
    private readonly DateTimePicker _startTime = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 100 };
    private readonly DateTimePicker _endTime = new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 100 };
    private readonly NumericUpDown _interval = new() { Minimum = 1, Maximum = 720, Value = 60 };
    private readonly CheckBox _weekdaysOnly = new() { Text = "Only Monday-Friday", AutoSize = true };
    private readonly CheckBox _startWithWindows = new() { Text = "Start with Windows", AutoSize = true };
    private readonly CheckBox _minimizeToTrayOnStart = new() { Text = "Start minimized to tray", AutoSize = true };
    private readonly CheckBox _showNotification = new() { Text = "Show desktop notification", AutoSize = true };
    private readonly CheckBox _flashWindow = new() { Text = "Flash window on trigger", AutoSize = true };
    private readonly CheckBox _repeatSiren = new() { Text = "Repeat siren", AutoSize = true };
    private readonly NumericUpDown _repeatCount = new() { Minimum = 1, Maximum = 20, Value = 3 };
    private readonly NumericUpDown _highFrequency = new() { Minimum = 200, Maximum = 4000, Value = 1400 };
    private readonly NumericUpDown _lowFrequency = new() { Minimum = 200, Maximum = 4000, Value = 850 };
    private readonly NumericUpDown _toneDurationMs = new() { Minimum = 50, Maximum = 2000, Value = 350 };
    private readonly NumericUpDown _pauseDurationMs = new() { Minimum = 0, Maximum = 1000, Value = 100 };
    private readonly TextBox _customSoundPath = new() { Width = 270 };
    private readonly Label _nextTriggerLabel = new() { AutoSize = true, Text = "Next trigger: calculating..." };
    private readonly Label _statusLabel = new() { AutoSize = true, Text = "Status: ready" };

    private DateTime _lastTrigger = DateTime.MinValue;
    private bool _allowClose;

    public MainForm(AppSettings settings)
    {
        _settings = settings;

        Text = "Elsun Siren Scheduler";
        Width = 700;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;

        var panel = BuildUi();
        Controls.Add(panel);

        _tickTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _tickTimer.Tick += (_, _) => OnTick();

        _tray = BuildTrayIcon();

        LoadSettingsToControls();
        UpdateStartupRegistration();
        _tickTimer.Start();

        FormClosing += MainForm_FormClosing;
        Resize += MainForm_Resize;

        if (_settings.MinimizeToTrayOnStart)
        {
            WindowState = FormWindowState.Minimized;
            Hide();
        }
    }

    private TableLayoutPanel BuildUi()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(12),
            ColumnCount = 3,
            RowCount = 22
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;
        table.Controls.Add(_enabled, 0, row++);
        table.SetColumnSpan(_enabled, 3);

        AddRow(table, row++, "Start time:", _startTime);
        AddRow(table, row++, "End time:", _endTime);
        AddRow(table, row++, "Interval (minutes):", _interval);

        table.Controls.Add(_weekdaysOnly, 0, row++);
        table.SetColumnSpan(_weekdaysOnly, 3);
        table.Controls.Add(_startWithWindows, 0, row++);
        table.SetColumnSpan(_startWithWindows, 3);
        table.Controls.Add(_minimizeToTrayOnStart, 0, row++);
        table.SetColumnSpan(_minimizeToTrayOnStart, 3);
        table.Controls.Add(_showNotification, 0, row++);
        table.SetColumnSpan(_showNotification, 3);
        table.Controls.Add(_flashWindow, 0, row++);
        table.SetColumnSpan(_flashWindow, 3);

        table.Controls.Add(_repeatSiren, 0, row++);
        table.SetColumnSpan(_repeatSiren, 3);
        AddRow(table, row++, "Repeat count:", _repeatCount);
        AddRow(table, row++, "High frequency (Hz):", _highFrequency);
        AddRow(table, row++, "Low frequency (Hz):", _lowFrequency);
        AddRow(table, row++, "Tone duration (ms):", _toneDurationMs);
        AddRow(table, row++, "Pause duration (ms):", _pauseDurationMs);

        var browseButton = new Button { Text = "Browse...", Width = 80 };
        browseButton.Click += (_, _) => BrowseSound();
        table.Controls.Add(new Label { Text = "Custom WAV (optional):", AutoSize = true }, 0, row);
        table.Controls.Add(_customSoundPath, 1, row);
        table.Controls.Add(browseButton, 2, row++);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        var saveButton = new Button { Text = "Save settings", Width = 120 };
        var testButton = new Button { Text = "Test siren now", Width = 120 };
        saveButton.Click += (_, _) => SaveFromControls();
        testButton.Click += (_, _) => TriggerAlarm(manualTest: true);
        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(testButton);
        table.Controls.Add(buttonPanel, 0, row++);
        table.SetColumnSpan(buttonPanel, 3);

        table.Controls.Add(_nextTriggerLabel, 0, row++);
        table.SetColumnSpan(_nextTriggerLabel, 3);

        table.Controls.Add(_statusLabel, 0, row++);
        table.SetColumnSpan(_statusLabel, 3);

        return table;
    }

    private static void AddRow(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        table.Controls.Add(control, 1, row);
        table.SetColumnSpan(control, 2);
    }

    private NotifyIcon BuildTrayIcon()
    {
        var tray = new NotifyIcon
        {
            Icon = SystemIcons.Warning,
            Text = "Elsun Siren",
            Visible = true
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open", null, (_, _) => ShowFromTray());
        menu.Items.Add("Trigger now", null, (_, _) => TriggerAlarm(manualTest: true));
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowFromTray();
        return tray;
    }

    private void LoadSettingsToControls()
    {
        _enabled.Checked = _settings.Enabled;
        _startTime.Value = DateTime.Today.Add(_settings.StartTime.ToTimeSpan());
        _endTime.Value = DateTime.Today.Add(_settings.EndTime.ToTimeSpan());
        _interval.Value = Math.Clamp(_settings.IntervalMinutes, 1, 720);
        _weekdaysOnly.Checked = _settings.OnlyWeekdays;
        _startWithWindows.Checked = _settings.StartWithWindows;
        _minimizeToTrayOnStart.Checked = _settings.MinimizeToTrayOnStart;
        _showNotification.Checked = _settings.ShowNotification;
        _flashWindow.Checked = _settings.FlashWindow;
        _repeatSiren.Checked = _settings.RepeatSiren;
        _repeatCount.Value = Math.Clamp(_settings.RepeatCount, 1, 20);
        _highFrequency.Value = Math.Clamp(_settings.HighFrequency, 200, 4000);
        _lowFrequency.Value = Math.Clamp(_settings.LowFrequency, 200, 4000);
        _toneDurationMs.Value = Math.Clamp(_settings.ToneDurationMs, 50, 2000);
        _pauseDurationMs.Value = Math.Clamp(_settings.PauseDurationMs, 0, 1000);
        _customSoundPath.Text = _settings.CustomSoundPath;
        UpdateNextTriggerLabel();
    }

    private void SaveFromControls()
    {
        var start = TimeOnly.FromDateTime(_startTime.Value);
        var end = TimeOnly.FromDateTime(_endTime.Value);
        if (start >= end)
        {
            MessageBox.Show("End time must be after start time.", "Elsun Siren", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.Enabled = _enabled.Checked;
        _settings.StartTime = start;
        _settings.EndTime = end;
        _settings.IntervalMinutes = (int)_interval.Value;
        _settings.OnlyWeekdays = _weekdaysOnly.Checked;
        _settings.StartWithWindows = _startWithWindows.Checked;
        _settings.MinimizeToTrayOnStart = _minimizeToTrayOnStart.Checked;
        _settings.ShowNotification = _showNotification.Checked;
        _settings.FlashWindow = _flashWindow.Checked;
        _settings.RepeatSiren = _repeatSiren.Checked;
        _settings.RepeatCount = (int)_repeatCount.Value;
        _settings.HighFrequency = (int)_highFrequency.Value;
        _settings.LowFrequency = (int)_lowFrequency.Value;
        _settings.ToneDurationMs = (int)_toneDurationMs.Value;
        _settings.PauseDurationMs = (int)_pauseDurationMs.Value;
        _settings.CustomSoundPath = _customSoundPath.Text.Trim();

        AppSettingsService.Save(_settings);
        UpdateStartupRegistration();
        UpdateNextTriggerLabel();

        _statusLabel.Text = "Status: settings saved.";
        MessageBox.Show("Settings saved.", "Elsun Siren", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void UpdateStartupRegistration()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null) return;

        if (_settings.StartWithWindows)
        {
            var appPath = Application.ExecutablePath;
            key.SetValue(AppName, $"\"{appPath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }

    private void OnTick()
    {
        if (!_settings.Enabled)
        {
            _statusLabel.Text = "Status: scheduler is disabled.";
            UpdateNextTriggerLabel();
            return;
        }

        var now = DateTime.Now;
        var nextTrigger = GetNextTrigger(now.AddSeconds(-1));

        if (nextTrigger is null)
        {
            _statusLabel.Text = "Status: no valid upcoming trigger.";
            _nextTriggerLabel.Text = "Next trigger: not found with current settings.";
            return;
        }

        _nextTriggerLabel.Text = $"Next trigger: {nextTrigger.Value:yyyy-MM-dd HH:mm:ss}";
        _statusLabel.Text = "Status: running.";

        if (now >= nextTrigger.Value && _lastTrigger != nextTrigger.Value)
        {
            _lastTrigger = nextTrigger.Value;
            TriggerAlarm(manualTest: false);
        }
    }

    private void TriggerAlarm(bool manualTest)
    {
        if (_settings.ShowNotification)
        {
            _tray.ShowBalloonTip(
                5000,
                "Elsun Siren",
                manualTest ? "Manual siren test started." : "Hourly siren reminder triggered.",
                ToolTipIcon.Warning);
        }

        if (_settings.FlashWindow)
        {
            BeginInvoke(() =>
            {
                BackColor = Color.OrangeRed;
                var flashTimer = new System.Windows.Forms.Timer { Interval = 600 };
                flashTimer.Tick += (_, _) =>
                {
                    BackColor = SystemColors.Control;
                    flashTimer.Stop();
                    flashTimer.Dispose();
                };
                flashTimer.Start();
            });
        }

        Task.Run(PlaySiren);
    }

    private void PlaySiren()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_settings.CustomSoundPath) && File.Exists(_settings.CustomSoundPath))
            {
                using var player = new SoundPlayer(_settings.CustomSoundPath);
                if (_settings.RepeatSiren)
                {
                    for (var i = 0; i < _settings.RepeatCount; i++)
                    {
                        player.PlaySync();
                        Thread.Sleep(_settings.PauseDurationMs);
                    }
                }
                else
                {
                    player.PlaySync();
                }

                return;
            }

            var loops = _settings.RepeatSiren ? _settings.RepeatCount : 1;
            for (var i = 0; i < loops; i++)
            {
                Console.Beep(_settings.HighFrequency, _settings.ToneDurationMs);
                Thread.Sleep(_settings.PauseDurationMs);
                Console.Beep(_settings.LowFrequency, _settings.ToneDurationMs);
                Thread.Sleep(_settings.PauseDurationMs);
            }
        }
        catch
        {
            SystemSounds.Exclamation.Play();
        }
    }

    private void BrowseSound()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "WAV files (*.wav)|*.wav|All files (*.*)|*.*",
            Title = "Select custom siren sound"
        };

        if (ofd.ShowDialog() == DialogResult.OK)
        {
            _customSoundPath.Text = ofd.FileName;
        }
    }

    private void UpdateNextTriggerLabel()
    {
        var next = GetNextTrigger(DateTime.Now);
        _nextTriggerLabel.Text = next is null
            ? "Next trigger: not found with current settings."
            : $"Next trigger: {next.Value:yyyy-MM-dd HH:mm:ss}";
    }

    private DateTime? GetNextTrigger(DateTime from)
    {
        var probe = from;
        for (var i = 0; i < 60 * 24 * 14; i++)
        {
            var day = probe.Date;
            if (_settings.OnlyWeekdays && (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                probe = day.AddDays(1).Add(_settings.StartTime.ToTimeSpan());
                continue;
            }

            var start = day.Add(_settings.StartTime.ToTimeSpan());
            var end = day.Add(_settings.EndTime.ToTimeSpan());

            if (from <= start)
            {
                return start;
            }

            if (from > end)
            {
                probe = day.AddDays(1).Add(_settings.StartTime.ToTimeSpan());
                continue;
            }

            var elapsedMinutes = (int)Math.Ceiling((from - start).TotalMinutes);
            var nextSlotMinutes = ((elapsedMinutes + _settings.IntervalMinutes - 1) / _settings.IntervalMinutes) * _settings.IntervalMinutes;
            var candidate = start.AddMinutes(nextSlotMinutes);
            if (candidate <= end)
            {
                return candidate;
            }

            probe = day.AddDays(1).Add(_settings.StartTime.ToTimeSpan());
        }

        return null;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _allowClose = true;
        _tray.Visible = false;
        Close();
    }

    private void MainForm_Resize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose || e.CloseReason == CloseReason.ApplicationExitCall)
        {
            return;
        }

        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            _tray.ShowBalloonTip(2000, "Elsun Siren", "Still running in system tray.", ToolTipIcon.Info);
        }
    }
}
