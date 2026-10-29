using Microsoft.UI.Dispatching;
using Wintor.Core;

namespace Wintor.App.Services;

/// <summary>
/// The side pane's Actions → "When done" choice for this run of Wintor. It is not saved: a PC that shuts itself down
/// every time Wintor starts again, because of a choice made days ago, would be a nasty surprise.
/// </summary>
public sealed class WhenDoneService
{
    private readonly ITorrentEngine _engine;
    private readonly WhenDoneWatch _watch = new();
    private readonly DispatcherQueueTimer _timer;

    public WhenDoneService(ITorrentEngine engine)
    {
        _engine = engine;
        // Created on the UI thread, so the timer ticks there too.
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(2);
        _timer.Tick += (_, _) => Check();
    }

    public WhenDoneRule Rule => _watch.Rule;

    /// <summary>The rule changed: picked from the menu, or reset to Off after it fired.</summary>
    public event Action? RuleChanged;

    /// <summary>The rule's condition is met (on the UI thread). The rule is Off again by now.</summary>
    public event Action<WhenDoneRule>? Triggered;

    public void Set(WhenDoneRule rule)
    {
        _watch.Set(rule);
        if (rule.IsOff)
        {
            _timer.Stop();
        }
        else
        {
            _timer.Start();
        }

        RuleChanged?.Invoke();
    }

    private void Check()
    {
        WhenDoneRule rule = _watch.Rule;
        if (!_watch.Check(_engine.GetSnapshots()))
        {
            return;
        }

        _timer.Stop();
        RuleChanged?.Invoke();
        Triggered?.Invoke(rule);
    }
}
