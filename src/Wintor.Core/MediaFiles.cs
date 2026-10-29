namespace Wintor.Core;

/// <summary>Which files a player can open while they download, and what to tell the player they are.</summary>
public static class MediaFiles
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".mkv"] = "video/x-matroska",
        [".webm"] = "video/webm",
        [".avi"] = "video/x-msvideo",
        [".mov"] = "video/quicktime",
        [".wmv"] = "video/x-ms-wmv",
        [".ts"] = "video/mp2t",
        [".m2ts"] = "video/mp2t",
        [".mpg"] = "video/mpeg",
        [".mpeg"] = "video/mpeg",
        [".flv"] = "video/x-flv",
        [".ogv"] = "video/ogg",
        [".3gp"] = "video/3gpp",
        [".mp3"] = "audio/mpeg",
        [".flac"] = "audio/flac",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".ogg"] = "audio/ogg",
        [".opus"] = "audio/opus",
        [".wav"] = "audio/wav",
        [".wma"] = "audio/x-ms-wma",
    };

    public static bool IsPlayable(string? path) =>
        !string.IsNullOrEmpty(path) && ContentTypes.ContainsKey(Path.GetExtension(path));

    /// <summary>The MIME type sent to the player; a generic one for anything else.</summary>
    public static string ContentType(string path) =>
        ContentTypes.TryGetValue(Path.GetExtension(path), out string? type) ? type : "application/octet-stream";

    /// <summary>
    /// The file "Play" on the whole torrent opens: the largest playable one (the film, not its sample or trailer),
    /// or null when there is none.
    /// </summary>
    public static int? MainFile(IEnumerable<(int Index, string Path, long Size)> files) =>
        files.Where(f => IsPlayable(f.Path))
            .OrderByDescending(f => f.Size)
            .Select(f => (int?)f.Index)
            .FirstOrDefault();
}
