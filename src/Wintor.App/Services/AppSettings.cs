using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wintor.App.Services;

/// <summary>User preferences stored as JSON next to the engine data.</summary>
public sealed class AppSettings
{
    private string _filePath = "";

    public string DownloadDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

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
        return settings;
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(this, AppSettingsJsonContext.Default.AppSettings));
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;
