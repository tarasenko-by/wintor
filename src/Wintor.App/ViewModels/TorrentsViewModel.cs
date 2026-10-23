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
    private Task? _initialization;

    public TorrentsViewModel(ITorrentEngine engine, AppSettings settings)
    {
        _engine = engine;
        _settings = settings;

        // The view model is created on the UI thread, so the timer ticks there too.
        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Refresh();
    }

    public ObservableCollection<TorrentItemViewModel> Torrents { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand), nameof(ResumeCommand))]
    public partial TorrentItemViewModel? SelectedTorrent { get; set; }

    public bool HasSelection => SelectedTorrent is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial bool IsLoading { get; set; } = true;

    public bool IsEmpty => !IsLoading && Torrents.Count == 0;

    public bool HasTorrents => Torrents.Count > 0;

    [ObservableProperty]
    public partial string TotalSpeedText { get; set; } = "";

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Starts the engine once; safe to call every time the page is shown.</summary>
    public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

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

    public async Task AddMagnetAsync(string magnetLink) =>
        await RunAsync(async () => Select(await _engine.AddMagnetAsync(magnetLink, _settings.DownloadDirectory)));

    public async Task AddTorrentFilesAsync(IEnumerable<string> paths)
    {
        foreach (string path in paths)
        {
            await RunAsync(async () => Select(await _engine.AddTorrentFileAsync(path, _settings.DownloadDirectory)));
        }
    }

    public async Task RemoveAsync(TorrentItemViewModel item, bool deleteFiles) =>
        await RunAsync(() => _engine.RemoveAsync(item.Id, deleteFiles));

    [RelayCommand(CanExecute = nameof(CanPause))]
    private Task PauseAsync() => RunAsync(() => _engine.PauseAsync(SelectedTorrent!.Id));

    private bool CanPause() => SelectedTorrent?.CanPause == true;

    [RelayCommand(CanExecute = nameof(CanResume))]
    private Task ResumeAsync() => RunAsync(() => _engine.StartAsync(SelectedTorrent!.Id));

    private bool CanResume() => SelectedTorrent is { CanPause: false };

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

    private void Select(string id)
    {
        Refresh();
        SelectedTorrent = Torrents.FirstOrDefault(t => t.Id == id);
    }

    private void Refresh()
    {
        IReadOnlyList<TorrentSnapshot> snapshots = _engine.GetSnapshots();

        for (int i = Torrents.Count - 1; i >= 0; i--)
        {
            if (!snapshots.Any(s => s.Id == Torrents[i].Id))
            {
                Torrents.RemoveAt(i);
            }
        }

        foreach (TorrentSnapshot snapshot in snapshots)
        {
            TorrentItemViewModel? item = Torrents.FirstOrDefault(t => t.Id == snapshot.Id);
            if (item is null)
            {
                item = new TorrentItemViewModel(snapshot.Id);
                Torrents.Add(item);
            }

            item.Update(snapshot);
        }

        TotalSpeedText = snapshots.Count > 0
            ? $"↓ {Formatting.Rate(snapshots.Sum(s => s.DownloadRate))}   ↑ {Formatting.Rate(snapshots.Sum(s => s.UploadRate))}"
            : "";
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasTorrents));
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
    }
}
