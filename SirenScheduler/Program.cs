using System.Text.Json;

namespace SirenScheduler;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        var settings = AppSettingsService.Load();
        Application.Run(new MainForm(settings));
    }
}

internal sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
    public TimeOnly StartTime { get; set; } = new(8, 0);
    public TimeOnly EndTime { get; set; } = new(20, 0);
    public bool OnlyWeekdays { get; set; }
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTrayOnStart { get; set; }
    public bool ShowNotification { get; set; } = true;
    public bool FlashWindow { get; set; } = true;
    public bool RepeatSiren { get; set; } = true;
    public int RepeatCount { get; set; } = 3;
    public int HighFrequency { get; set; } = 1400;
    public int LowFrequency { get; set; } = 850;
    public int ToneDurationMs { get; set; } = 350;
    public int PauseDurationMs { get; set; } = 100;
    public string CustomSoundPath { get; set; } = string.Empty;
}

internal static class AppSettingsService
{
    private static readonly string SettingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ElsunSiren");
    private static readonly string SettingsPath = Path.Combine(SettingsFolder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var text = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(text);
            return settings ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsFolder);
        var text = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, text);
    }
}
