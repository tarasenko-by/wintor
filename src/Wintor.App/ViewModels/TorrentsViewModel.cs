using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Wintor.App.Services;
using Wintor.Core;

namespace Wintor.App.ViewModels;

public partial class TorrentsViewModel : ObservableObject
{
    private readonly ITorrentEngine _engine;
    private readonly AppSettings _settings;
    private readonly DispatcherQueueTimer _timer;
    private readonly Dictionary<string, TorrentItemViewModel> _items = [];
    private readonly Queue<AddRequest> _pendingAdds = [];
    private IReadOnlyList<TorrentSnapshot> _snapshots = [];
    private Task? _initialization;
    private bool _refreshedOnce;

    public TorrentsViewModel(ITorrentEngine engine, AppSettings settings)
    {
        _engine = engine;
        _settings = settings;
        Sort = settings.Sort;
        SortDescending = settings.SortDescending;
        foreach (string label in settings.Labels)
        {
            Labels.Add(new LabelItem(label));
        }

        // Created on the UI thread, so the timer ticks there too.
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Refresh();
    }

    /// <summary>A torrent finished downloading while Wintor was running.</summary>
    public event Action<TorrentSnapshot>? Completed;

    /// <summary>An add from outside (file or magnet link) is waiting for the page to show its dialog.</summary>
    public event Action? AddRequested;

    /// <summary>The rows that pass the current view and search, sorted.</summary>
    public ObservableCollection<TorrentItemViewModel> Torrents { get; } = [];

    /// <summary>Labels for the side pane, in the user's order.</summary>
    public ObservableCollection<LabelItem> Labels { get; } = [];

    /// <summary>Set by the page from the table's selection (several with Ctrl/Shift).</summary>
    public IReadOnlyList<TorrentItemViewModel> SelectedTorrents { get; private set; } = [];

    public bool HasSelection => SelectedTorrents.Count > 0;

    public string DownloadDirectory => _settings.DownloadDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(IsNothingFound))]
    public partial bool IsLoading { get; set; } = true;

    /// <summary>The status group shown; ignored while <see cref="LabelFilter"/> is set.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewTitle))]
    public partial TorrentFilter Filter { get; set; }

    /// <summary>Show only torrents with this label (a label picked in the side pane).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViewTitle))]
    public partial string? LabelFilter { get; set; }

    [ObservableProperty]
    public partial string Search { get; set; } = "";

    [ObservableProperty]
    public partial TorrentSort Sort { get; set; }

    [ObservableProperty]
    public partial bool SortDescending { get; set; }

    [ObservableProperty]
    public partial int AllCount { get; set; }

    [ObservableProperty]
    public partial int DownloadingCount { get; set; }

    [ObservableProperty]
    public partial int CompletedCount { get; set; }

    [ObservableProperty]
    public partial int PausedCount { get; set; }

    public string ViewTitle => LabelFilter ?? Strings.Get("View_" + Filter);

    /// <summary>No torrents at all: the welcome state.</summary>
    public bool IsEmpty => !IsLoading && _items.Count == 0;

    /// <summary>There are torrents, but none passes the view or search.</summary>
    public bool IsNothingFound => !IsLoading && _items.Count > 0 && Torrents.Count == 0;

    public bool HasVisibleTorrents => Torrents.Count > 0;

    /// <summary>"3 torrents · ↓ 1.2 MB/s ↑ 20 KB/s" under the page title.</summary>
    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    /// <summary>Total speed, or empty when nothing moves (for the tray tooltip).</summary>
    [ObservableProperty]
    public partial string TotalSpeedText { get; set; } = "";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Starts the engine once; safe to call again.</summary>
    public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

    public void ShowFilter(TorrentFilter filter)
    {
        LabelFilter = null;
        Filter = filter;
    }

    public void ShowLabel(string label)
    {
        Filter = TorrentFilter.All;
        LabelFilter = label;
    }

    public void SetSelection(IReadOnlyList<TorrentItemViewModel> selection)
    {
        SelectedTorrents = selection;
        OnPropertyChanged(nameof(HasSelection));
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
    }

    /// <summary>A column header was clicked: sort by it, or flip the direction if it already is.</summary>
    public void SortBy(TorrentSort column)
    {
        if (Sort == column)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            Sort = column;
            // Numbers people scan for "most" read best biggest-first; names and the queue A→Z / 1→n.
            SortDescending = column is TorrentSort.Size or TorrentSort.Progress or TorrentSort.DownloadSpeed
                or TorrentSort.UploadSpeed or TorrentSort.Seeds or TorrentSort.Peers;
        }
    }

    // --- Adding ---

    public void RequestAdd(AddRequest request)
    {
        _pendingAdds.Enqueue(request);
        AddRequested?.Invoke();
    }

    public bool TryTakePendingAdd(out AddRequest request) => _pendingAdds.TryDequeue(out request!);

    public Task<TorrentMetadata> ReadTorrentFileAsync(string path) => _engine.ReadTorrentFileAsync(path);

    public async Task AddMagnetAsync(string magnetLink, string? folder = null) =>
        await RunAsync(async () => Reveal(await _engine.AddMagnetAsync(magnetLink, folder ?? _settings.DownloadDirectory)));

    public async Task AddTorrentFileAsync(string path, string? folder = null, IReadOnlySet<int>? skippedFiles = null) =>
        await RunAsync(async () => Reveal(await _engine.AddTorrentFileAsync(path, folder ?? _settings.DownloadDirectory, skippedFiles)));

    // --- Actions ---

    public async Task RemoveAsync(IReadOnlyList<TorrentItemViewModel> items, bool deleteFiles) =>
        await RunAsync(async () =>
        {
            foreach (TorrentItemViewModel item in items)
            {
                await _engine.RemoveAsync(item.Id, deleteFiles);
                _settings.Queue.Remove(item.Id);
                _settings.TorrentLabels.Remove(item.Id);
            }

            _settings.Save();
        });

    public Task PauseAsync(IEnumerable<TorrentItemViewModel> items) =>
        RunAsync(async () =>
        {
            foreach (TorrentItemViewModel item in items.Where(i => i.CanPause).ToList())
            {
                await _engine.PauseAsync(item.Id);
            }
        });

    public Task ResumeAsync(IEnumerable<TorrentItemViewModel> items) =>
        RunAsync(async () =>
        {
            foreach (TorrentItemViewModel item in items.Where(i => !i.CanPause).ToList())
            {
                await _engine.StartAsync(item.Id);
            }
        });

    public Task PauseAllAsync() => PauseAsync(_items.Values);

    public Task ResumeAllAsync() => ResumeAsync(_items.Values);

    [RelayCommand(CanExecute = nameof(CanPause))]
    private Task PauseSelectedAsync() => PauseAsync(SelectedTorrents);

    private bool CanPause() => SelectedTorrents.Any(t => t.CanPause);

    [RelayCommand(CanExecute = nameof(CanResume))]
    private Task ResumeSelectedAsync() => ResumeAsync(SelectedTorrents);

    private bool CanResume() => SelectedTorrents.Any(t => !t.CanPause);

    // Generated command names read better without "Selected" in XAML.
    public IAsyncRelayCommand PauseCommand => PauseSelectedCommand;

    public IAsyncRelayCommand ResumeCommand => ResumeSelectedCommand;

    /// <summary>
    /// Moves torrents in the user's order (the № column): by <paramref name="step"/> places, or to the very top
    /// (int.MinValue) / bottom (int.MaxValue). Switches the table to that order so the move is visible.
    /// </summary>
    public void Move(IReadOnlyList<TorrentItemViewModel> items, int step)
    {
        List<string> queue = _settings.Queue;
        var moving = items.Select(i => i.Id).Where(queue.Contains).ToList();
        if (moving.Count == 0)
        {
            return;
        }

        if (step is int.MinValue or int.MaxValue)
        {
            queue.RemoveAll(moving.Contains);
            queue.InsertRange(step == int.MinValue ? 0 : queue.Count, moving);
        }
        else
        {
            // Move one at a time, nearest to the destination first, so a selected block keeps its shape.
            IEnumerable<string> order = step < 0 ? moving.OrderBy(queue.IndexOf) : moving.OrderByDescending(queue.IndexOf);
            foreach (string id in order)
            {
                int from = queue.IndexOf(id);
                int to = Math.Clamp(from + step, 0, queue.Count - 1);
                queue.RemoveAt(from);
                queue.Insert(to, id);
            }
        }

        _settings.Save();
        Sort = TorrentSort.Queue;
        SortDescending = false;
        Refresh();
    }

    // --- Labels ---

    public void SetLabel(IReadOnlyList<TorrentItemViewModel> items, string? label)
    {
        label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        if (label is not null && !_settings.Labels.Contains(label, StringComparer.CurrentCultureIgnoreCase))
        {
            _settings.Labels.Add(label);
            Labels.Add(new LabelItem(label));
        }

        foreach (TorrentItemViewModel item in items)
        {
            if (label is null)
            {
                _settings.TorrentLabels.Remove(item.Id);
            }
            else
            {
                _settings.TorrentLabels[item.Id] = _settings.Labels.First(l => string.Equals(l, label, StringComparison.CurrentCultureIgnoreCase));
            }
        }

        _settings.Save();
        Refresh();
    }

    public void DeleteLabel(string label)
    {
        _settings.Labels.Remove(label);
        foreach (string id in _settings.TorrentLabels.Where(p => p.Value == label).Select(p => p.Key).ToList())
        {
            _settings.TorrentLabels.Remove(id);
        }

        _settings.Save();
        if (Labels.FirstOrDefault(l => l.Name == label) is { } item)
        {
            Labels.Remove(item);
        }

        if (LabelFilter == label)
        {
            ShowFilter(TorrentFilter.All);
        }

        Refresh();
    }

    partial void OnFilterChanged(TorrentFilter value) => Refresh();

    partial void OnLabelFilterChanged(string? value) => Refresh();

    partial void OnSearchChanged(string value) => Refresh();

    partial void OnSortChanged(TorrentSort value) => SaveSort();

    partial void OnSortDescendingChanged(bool value) => SaveSort();

    private void SaveSort()
    {
        _settings.Sort = Sort;
        _settings.SortDescending = SortDescending;
        _settings.Save();
        Refresh();
    }

    private async Task InitializeCoreAsync()
    {
        try
        {
            await _engine.InitializeAsync();
        }
        catch (Exception e)
        {
            ErrorMessage = Strings.Format("Error_EngineStart", e.Message);
        }

        IsLoading = false;
        Refresh();
        _timer.Start();
    }

    private async Task RunAsync(Func<Task> action)
    {
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception e)
        {
            ErrorMessage = e.Message;
        }

        Refresh();
    }

    /// <summary>A new torrent should be visible: drop a view or search that would hide it.</summary>
    private void Reveal(string id)
    {
        Refresh();
        if (_items.TryGetValue(id, out TorrentItemViewModel? item) && !Torrents.Contains(item))
        {
            Search = "";
            ShowFilter(TorrentFilter.All);
        }
    }

    private void Refresh()
    {
        if (_engine is null)
        {
            return; // property callbacks during construction
        }

        _snapshots = _engine.GetSnapshots();
        UpdateQueue();

        var seen = new HashSet<string>();
        foreach (TorrentSnapshot snapshot in _snapshots)
        {
            seen.Add(snapshot.Id);
            if (!_items.TryGetValue(snapshot.Id, out TorrentItemViewModel? item))
            {
                item = new TorrentItemViewModel(snapshot.Id);
                _items[snapshot.Id] = item;
            }
            else if (_refreshedOnce && !item.IsComplete && snapshot.IsComplete)
            {
                Completed?.Invoke(snapshot);
            }

            _settings.TorrentLabels.TryGetValue(snapshot.Id, out string? label);
            item.Update(snapshot, _settings.Queue.IndexOf(snapshot.Id) + 1, label);
        }

        foreach (string gone in _items.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            _items.Remove(gone);
        }

        _refreshedOnce = !IsLoading;
        SyncVisible();
        UpdateCounts();
    }

    /// <summary>New torrents join the end of the user's order; removed ones leave it.</summary>
    private void UpdateQueue()
    {
        var ids = _snapshots.Select(s => s.Id).ToHashSet();
        bool changed = _settings.Queue.RemoveAll(id => !ids.Contains(id)) > 0;
        foreach (TorrentSnapshot s in _snapshots)
        {
            if (!_settings.Queue.Contains(s.Id))
            {
                _settings.Queue.Add(s.Id);
                changed = true;
            }
        }

        if (changed && !IsLoading)
        {
            _settings.Save();
        }
    }

    private bool InView(TorrentSnapshot s)
    {
        if (LabelFilter is not null)
        {
            return _settings.TorrentLabels.TryGetValue(s.Id, out string? label) && label == LabelFilter
                && TorrentQuery.MatchesSearch(s.Name, Search);
        }

        return TorrentQuery.Matches(s, Filter, Search);
    }

    /// <summary>Updates the visible list in place (moves, inserts, removes) so selection and scroll survive.</summary>
    private void SyncVisible()
    {
        List<TorrentItemViewModel> wanted = TorrentQuery.Sort(_snapshots.Where(InView).ToList(), Sort, SortDescending, _settings.Queue)
            .Select(s => _items[s.Id])
            .ToList();
        var wantedSet = wanted.ToHashSet();

        for (int i = Torrents.Count - 1; i >= 0; i--)
        {
            if (!wantedSet.Contains(Torrents[i]))
            {
                Torrents.RemoveAt(i);
            }
        }

        for (int i = 0; i < wanted.Count; i++)
        {
            int current = Torrents.IndexOf(wanted[i]);
            if (current == -1)
            {
                Torrents.Insert(i, wanted[i]);
            }
            else if (current != i)
            {
                Torrents.Move(current, i);
            }
        }
    }

    private void UpdateCounts()
    {
        AllCount = _snapshots.Count;
        DownloadingCount = _snapshots.Count(s => TorrentQuery.MatchesFilter(s, TorrentFilter.Downloading));
        CompletedCount = _snapshots.Count(s => TorrentQuery.MatchesFilter(s, TorrentFilter.Completed));
        PausedCount = _snapshots.Count(s => TorrentQuery.MatchesFilter(s, TorrentFilter.Paused));
        foreach (LabelItem label in Labels)
        {
            label.Count = _settings.TorrentLabels.Count(p => p.Value == label.Name);
        }

        long down = _snapshots.Sum(s => s.DownloadRate), up = _snapshots.Sum(s => s.UploadRate);
        TotalSpeedText = down + up > 0 ? $"↓ {Formatting.Rate(down)}   ↑ {Formatting.Rate(up)}" : "";
        int shown = Torrents.Count;
        string count = Strings.Format(shown == 1 ? "Summary_OneTorrent" : "Summary_Torrents", shown);
        SummaryText = _snapshots.Count == 0 ? "" : TotalSpeedText.Length > 0 ? $"{count} · {TotalSpeedText}" : count;

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsNothingFound));
        OnPropertyChanged(nameof(HasVisibleTorrents));
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
    }
}
