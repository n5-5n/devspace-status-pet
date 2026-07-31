using System.Text.Json;
using DevSpaceStatusPet.Models;

namespace DevSpaceStatusPet.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _sync = new();
    private AppSettings _current;

    public SettingsStore()
    {
        _current = LoadCore();
    }

    public event EventHandler? Changed;

    public AppSettings Current
    {
        get
        {
            lock (_sync)
            {
                return _current.Clone();
            }
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.Normalize();

        lock (_sync)
        {
            Directory.CreateDirectory(AppPaths.DevSpaceDirectory);
            var tempPath = $"{AppPaths.SettingsPath}.tmp.{Environment.ProcessId}";
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(tempPath, json, new System.Text.UTF8Encoding(false));
            File.Move(tempPath, AppPaths.SettingsPath, true);
            _current = settings.Clone();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static AppSettings LoadCore()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(AppPaths.SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }
}

public sealed class PositionStore
{
    private sealed class PositionModel
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public Point? Load()
    {
        try
        {
            if (!File.Exists(AppPaths.PositionPath))
            {
                return null;
            }

            var model = JsonSerializer.Deserialize<PositionModel>(File.ReadAllText(AppPaths.PositionPath));
            return model is null ? null : new Point(model.X, model.Y);
        }
        catch
        {
            return null;
        }
    }

    public void Save(Point location)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DevSpaceDirectory);
            var model = new PositionModel { X = location.X, Y = location.Y };
            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.PositionPath, json, new System.Text.UTF8Encoding(false));
        }
        catch
        {
            // Position persistence is non-critical.
        }
    }
}
