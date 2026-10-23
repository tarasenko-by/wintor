using Microsoft.Windows.ApplicationModel.Resources;

namespace Wintor.App.Services;

/// <summary>Localized strings for code; XAML uses x:Uid. Text lives in Strings/&lt;language&gt;/Resources.resw.</summary>
public static class Strings
{
    private static readonly ResourceLoader Loader = new();

    public static string Get(string key)
    {
        string value = Loader.GetString(key);
        return string.IsNullOrEmpty(value) ? key : value;
    }

    public static string Format(string key, params object?[] args) => string.Format(Get(key), args);
}
