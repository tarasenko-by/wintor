namespace Wintor.Core;

public sealed class EngineOptions
{
    /// <summary>Where the engine keeps its state, resume data and metadata cache.</summary>
    public required string DataDirectory { get; init; }

    /// <summary>TCP/UDP port for incoming peer connections.</summary>
    public int ListenPort { get; init; } = 52413;

    /// <summary>Bytes per second; 0 means unlimited.</summary>
    public int MaxDownloadRate { get; init; }

    /// <summary>Bytes per second; 0 means unlimited.</summary>
    public int MaxUploadRate { get; init; }
}
