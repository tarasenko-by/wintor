using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Wintor.Core;

namespace Wintor.App.Services;

/// <summary>
/// "Play": a finished file opens in its default app like a double-click; a file still downloading is streamed and
/// its link handed to a player that can open links.
/// </summary>
public static class Playback
{
    // Players that open a link given on the command line, for when the default app cannot (Media Player, Films & TV:
    // packaged apps whose handler takes files only).
    private static readonly string[] KnownPlayers = ["vlc.exe", "mpv.exe", "mpc-hc64.exe", "mpc-be64.exe", "PotPlayerMini64.exe"];

    /// <returns>Null when a player opened; otherwise the stream link, because no installed player can open links.</returns>
    public static async Task<Uri?> PlayAsync(ITorrentEngine engine, string id, int fileIndex)
    {
        TorrentDetails details = await engine.GetDetailsAsync(id) ?? throw new KeyNotFoundException(id);
        TorrentFileEntry file = details.Files.First(f => f.Index == fileIndex);
        if (file.IsComplete && File.Exists(file.FullPath))
        {
            Process.Start(new ProcessStartInfo(file.FullPath) { UseShellExecute = true });
            return null;
        }

        Uri link = await engine.StreamFileAsync(id, fileIndex);
        if (FindPlayer(Path.GetExtension(file.Path), link.AbsoluteUri) is not { } player)
        {
            return link;
        }

        Process.Start(new ProcessStartInfo(player.Program, player.Arguments) { UseShellExecute = false });
        return null;
    }

    /// <summary>The default app for the file type if it takes a link, otherwise the first known player installed.</summary>
    private static (string Program, string Arguments)? FindPlayer(string extension, string url)
    {
        if (PlayerCommand.Build(DefaultCommand(extension), url) is { } command && File.Exists(command.Program))
        {
            return command;
        }

        foreach (string exe in KnownPlayers)
        {
            if (AppPath(exe) is { } program)
            {
                return (program, "\"" + url + "\"");
            }
        }

        return null;
    }

    private static string? DefaultCommand(string extension)
    {
        const uint AssocFNoTruncate = 0x20, AssocStrCommand = 1;
        uint size = 2048;
        var buffer = new StringBuilder((int)size);
        return AssocQueryString(AssocFNoTruncate, AssocStrCommand, extension, "open", buffer, ref size) == 0
            ? buffer.ToString()
            : null;
    }

    /// <summary>Where Windows' "App Paths" say a program is installed (what Win+R uses to find it).</summary>
    private static string? AppPath(string exe)
    {
        foreach (RegistryKey root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using RegistryKey? key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe);
            if (key?.GetValue(null) is string path && File.Exists(path = Environment.ExpandEnvironmentVariables(path.Trim('"'))))
            {
                return path;
            }
        }

        return null;
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryString(uint flags, uint str, string assoc, string? extra, StringBuilder? output, ref uint size);
}
