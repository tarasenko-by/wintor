namespace Wintor.Core;

/// <summary>A reason a torrent stopped that the user can act on.</summary>
public enum TorrentProblem
{
    None,

    /// <summary>Downloaded files were moved or deleted. Resuming checks what is left and downloads the rest.</summary>
    FilesMissing,

    /// <summary>The drive is full. Resuming after freeing space continues.</summary>
    DiskFull,

    /// <summary>Windows denied access to the download folder.</summary>
    NoAccess,

    /// <summary>Any other failure; the engine's message is shown as is.</summary>
    Other,
}

public static class TorrentProblems
{
    // Win32 error codes as they appear in the low word of an IOException's HResult.
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorHandleDiskFull = 39;
    private const int ErrorDiskFull = 112;

    /// <summary>Maps an engine failure to a problem the UI can explain. Looks through wrapping exceptions.</summary>
    public static TorrentProblem Classify(Exception? exception)
    {
        if (exception is null)
        {
            return TorrentProblem.None;
        }

        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case UnauthorizedAccessException:
                    return TorrentProblem.NoAccess;
                case DirectoryNotFoundException or FileNotFoundException:
                    return TorrentProblem.FilesMissing;
                case IOException io:
                    switch (io.HResult & 0xFFFF)
                    {
                        case ErrorDiskFull or ErrorHandleDiskFull:
                            return TorrentProblem.DiskFull;
                        case ErrorAccessDenied:
                            return TorrentProblem.NoAccess;
                        case ErrorFileNotFound or ErrorPathNotFound:
                            return TorrentProblem.FilesMissing;
                    }

                    break;
            }
        }

        return TorrentProblem.Other;
    }
}
