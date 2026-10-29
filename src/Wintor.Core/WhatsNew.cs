namespace Wintor.Core;

/// <summary>One version in "What's new": its number, its date and the string keys of its points.</summary>
public sealed record Release(Version Version, DateOnly Date, IReadOnlyList<string> Keys)
{
    /// <summary>"0.2" for 0.2.0, "0.2.1": how release notes name a version.</summary>
    public string VersionText => WhatsNew.Display(Version);
}

/// <summary>What to do at launch about "What's new".</summary>
public enum WhatsNewStep
{
    /// <summary>This version (or a newer one) was seen already.</summary>
    None,

    /// <summary>Store the version as seen without showing anything (first launch, the switch off, no entry for it).</summary>
    Remember,

    /// <summary>Show the dialog, then store the version as seen.</summary>
    Show,
}

/// <summary>
/// The changes of each version, newest first, shown once after an update and from Settings → About. The points are
/// app strings (WhatsNew_*) in every language. A new version adds its release on top.
/// </summary>
public static class WhatsNew
{
    public static IReadOnlyList<Release> Releases { get; } =
    [
        new(new Version(0, 3, 0), new DateOnly(2026, 10, 29),
            ["WhatsNew_030_Play", "WhatsNew_030_Order", "WhatsNew_030_WhenDone"]),
        new(new Version(0, 2, 0), new DateOnly(2026, 10, 24),
            ["WhatsNew_020_Table", "WhatsNew_020_Details", "WhatsNew_020_Labels", "WhatsNew_020_Limits",
             "WhatsNew_020_Problems", "WhatsNew_020_Languages"]),
    ];

    /// <summary>The release of <paramref name="version"/> (its revision ignored), or null when it has none.</summary>
    public static Release? For(Version version) => Releases.FirstOrDefault(r => r.Version == Normalize(version));

    /// <summary>
    /// What to do at launch. Without a <paramref name="lastSeen"/> version (a new install, or the first launch of a
    /// build that has "What's new") the version is only remembered: there is no older Wintor to compare with. A newer
    /// version is shown when it has an entry and the switch in Settings is on.
    /// </summary>
    public static WhatsNewStep Decide(Version current, string? lastSeen, bool enabled)
    {
        current = Normalize(current);
        if (!Version.TryParse(lastSeen, out Version? seen))
        {
            return WhatsNewStep.Remember;
        }

        if (current <= Normalize(seen))
        {
            return WhatsNewStep.None;
        }

        return enabled && For(current) is not null ? WhatsNewStep.Show : WhatsNewStep.Remember;
    }

    /// <summary>How the version is stored as seen: "0.2.1".</summary>
    public static string Stored(Version version) => Normalize(version).ToString(3);

    /// <summary>"0.2" for 0.2.0, else "0.2.1".</summary>
    public static string Display(Version version) =>
        Normalize(version) is var v && v.Build == 0 ? v.ToString(2) : v.ToString(3);

    /// <summary>Major.minor.build: a package's revision (0.2.1.0) and a missing build (0.2) do not count.</summary>
    public static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
