using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Wintor.App.Services;

/// <summary>Puts Windows to sleep, hibernates, restarts or shuts it down ("When done" in the side pane's Actions).</summary>
public static class Power
{
    // Offsets of BOOLEAN fields in SYSTEM_POWER_CAPABILITIES (winnt.h); only the first bytes are read.
    private const int SystemS1 = 3, SystemS2 = 4, SystemS3 = 5, SystemS4 = 6, HiberFilePresent = 8, AoAc = 20;

    private static readonly Lazy<byte[]?> Capabilities = new(() =>
    {
        var buffer = new byte[256];
        return GetPwrCapabilities(buffer) ? buffer : null;
    });

    /// <summary>The PC can sleep (S1–S3, or Modern Standby). Unknown counts as yes: the menu should not lose it.</summary>
    public static bool CanSleep => Capabilities.Value is not { } c || c[SystemS1] != 0 || c[SystemS2] != 0 || c[SystemS3] != 0 || c[AoAc] != 0;

    /// <summary>Hibernation is turned on (it is off on many PCs: <c>powercfg /hibernate off</c>, or Modern Standby).</summary>
    public static bool CanHibernate => Capabilities.Value is { } c && c[SystemS4] != 0 && c[HiberFilePresent] != 0;

    public static void Sleep()
    {
        // Modern Standby PCs have no S3 to suspend to; turning the screen off is how they go to sleep.
        if (Capabilities.Value is { } c && c[AoAc] != 0 && c[SystemS3] == 0)
        {
            PostMessageW(HwndBroadcast, WmSysCommand, ScMonitorPower, MonitorOff);
            return;
        }

        SetSuspendState(hibernate: false, force: false, wakeupEventsDisabled: false);
    }

    public static void Hibernate() => SetSuspendState(hibernate: true, force: false, wakeupEventsDisabled: false);

    /// <summary>Like Start → Power → Restart: apps get to close; Wintor has stopped its torrents by then.</summary>
    public static void Restart() => RunShutdown("/r /t 0");

    public static void ShutDown() => RunShutdown("/s /t 0");

    private static void RunShutdown(string arguments)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "shutdown.exe"), arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch (Exception)
        {
            // Blocked by policy: Wintor has quit already, and the PC simply stays on.
        }
    }

    private static readonly nint HwndBroadcast = 0xFFFF;
    private const uint WmSysCommand = 0x0112;
    private const nint ScMonitorPower = 0xF170;
    private const nint MonitorOff = 2;

    [DllImport("powrprof.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool GetPwrCapabilities([Out] byte[] capabilities);

    [DllImport("powrprof.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool SetSuspendState(
        [MarshalAs(UnmanagedType.U1)] bool hibernate,
        [MarshalAs(UnmanagedType.U1)] bool force,
        [MarshalAs(UnmanagedType.U1)] bool wakeupEventsDisabled);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(nint hwnd, uint msg, nint wParam, nint lParam);
}
