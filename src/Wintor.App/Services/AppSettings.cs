using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Wintor.Core;

namespace Wintor.App.Services;

/// <summary>What the window lies on. None is the solid palette (the default).</summary>
public enum WindowBackdrop
{
    None,
    Mica,
    MicaAlt,
    Acrylic,
    AcrylicThin,
}

/// <summary>User preferences stored as JSON next to the engine data.</summary>
public sealed class AppSettings
{
    public const int DefaultListenPort = 52413;

    private string _filePath = "";

    public string DownloadDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

    public ElementTheme Theme { get; set; } = ElementTheme.Default;

    public WindowBackdrop Backdrop { get; set; } = WindowBackdrop.None;

    /// <summary>KiB/s; 0 means unlimited.</summary>
    public int MaxDownloadKiB { get; set; }

    /// <summary>KiB/s; 0 means unlimited.</summary>
    public int MaxUploadKiB { get; set; }

    public int ListenPort { get; set; } = DefaultListenPort;

    /// <summary>Closing the window keeps Wintor seeding in the notification area.</summary>
    public bool CloseToTray { get; set; } = true;

    public bool NotifyWhenComplete { get; set; } = true;

    /// <summary>Interface language as a Strings/ folder name ("ru-RU"), or empty to follow Windows.</summary>
    public string Language { get; set; } = "";

    /// <summary>Open "What's new" once after an update.</summary>
    public bool ShowWhatsNew { get; set; } = true;

    /// <summary>The last version whose "What's new" was shown or skipped ("0.2.0"); empty before the first launch.</summary>
    public string WhatsNewSeenVersion { get; set; } = "";


    /// <summary>The user's own order of torrents (the № column), by id.</summary>
    public List<string> Queue { get; set; } = [];

    public TorrentSort Sort { get; set; } = TorrentSort.Queue;

    public bool SortDescending { get; set; }

    /// <summary>Label names, including ones no torrent has right now.</summary>
    public List<string> Labels { get; set; } = [];

    /// <summary>Label name → color key (Services/LabelColors).</summary>
    public Dictionary<string, string> LabelColors { get; set; } = [];

    /// <summary>Torrent id → label name.</summary>
    public Dictionary<string, string> TorrentLabels { get; set; } = [];

    /// <summary>Table columns the user turned off (see ViewModels/TableColumns for the keys).</summary>
    public List<string> HiddenColumns { get; set; } = ["TimeLeft", "Seeds", "Peers"];

    /// <summary>Column widths the user dragged, in DIPs, by column key.</summary>
    public Dictionary<string, double> ColumnWidths { get; set; } = [];

    /// <summary>Height of the details pane under the torrent table, in DIPs.</summary>
    public double DetailsPaneHeight { get; set; } = 300;

    /// <summary>Where the engine keeps its state; not saved, set at startup.</summary>
    [JsonIgnore]
    public string EngineDirectory { get; private set; } = "";

    public EngineOptions ToEngineOptions() => new()
    {
        DataDirectory = EngineDirectory,
        ListenPort = ListenPort is >= 1 and <= 65535 ? ListenPort : DefaultListenPort,
        MaxDownloadRate = Math.Max(MaxDownloadKiB, 0) * 1024,
        MaxUploadRate = Math.Max(MaxUploadKiB, 0) * 1024,
    };

    public static AppSettings Load(string dataDirectory)
    {
        string path = Path.Combine(dataDirectory, "settings.json");
        AppSettings settings;
        try
        {
            settings = File.Exists(path)
                ? JsonSerializer.Deserialize(File.ReadAllText(path), AppSettingsJsonContext.Default.AppSettings) ?? new()
                : new();
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            settings = new();
        }

        settings._filePath = path;
        settings.EngineDirectory = Path.Combine(dataDirectory, "engine");
        return settings;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings));
        }
        catch (IOException)
        {
            // A settings file that cannot be written (disk full, locked) must not crash the app; the change still applies.
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;
