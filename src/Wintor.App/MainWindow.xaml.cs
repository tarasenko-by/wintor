using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using Wintor.App.Services;
using Wintor.App.ViewModels;
using Wintor.App.Views;
using Wintor.Core;

namespace Wintor.App;

/// <summary>Implemented by pages that use the search box in the title bar.</summary>
public interface ISearchablePage
{
    void ApplySearch(string text);
}

public sealed partial class MainWindow : Window
{
    private readonly AppSettings _settings = App.Services.GetRequiredService<AppSettings>();
    private readonly TorrentsViewModel _torrents = App.Services.GetRequiredService<TorrentsViewModel>();
    private readonly NotificationService _notifications = App.Services.GetRequiredService<NotificationService>();
    private TrayIcon? _tray;
    private bool _exiting;
    private bool _engineStopped;

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = 640;
            presenter.PreferredMinimumHeight = 480;
        }

        double scale = GetDpiForWindow(Win32Interop.GetWindowFromWindowId(AppWindow.Id)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1280 * scale), (int)(800 * scale)));

        HighContrast.Initialize(AppWindow.Id);
        RootGrid.RequestedTheme = _settings.Theme;
        RootGrid.ActualThemeChanged += (_, _) => UpdateCaptionButtons();
        HighContrast.Changed += (_, _) => DispatcherQueue.TryEnqueue(UpdateCaptionButtons);
        UpdateCaptionButtons();
        Appearance.Attach(this, RootGrid, _settings);

        // TextChanged misses text set through UI Automation (dictation, assistive tools); the property never does.
        SearchBox.RegisterPropertyChangedCallback(AutoSuggestBox.TextProperty, (_, _) => ApplySearch());

        AppWindow.Closing += AppWindow_Closing;
        _torrents.Completed += OnTorrentCompleted;
        SetupTray();

        SyncLabelItems();
        _torrents.Labels.CollectionChanged += (_, _) => SyncLabelItems();
        NavView.SelectedItem = NavAll;
    }

    public ElementTheme Theme
    {
        get => RootGrid.RequestedTheme;
        set
        {
            RootGrid.RequestedTheme = value;
            UpdateCaptionButtons();
        }
    }

    /// <summary>Brings the window back from the tray (or from behind other windows).</summary>
    public void ShowAndActivate()
    {
        AppWindow.Show();
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        Activate();
    }

    /// <summary>For x:Bind in the side pane (counts).</summary>
    public TorrentsViewModel Torrents => _torrents;

    public void ShowTorrents()
    {
        if (ContentFrame.Content is not TorrentsPage)
        {
            NavView.SelectedItem = NavAll;
        }
    }

    /// <summary>Really quits (the tray's Exit, or a session ending), instead of hiding to the tray.</summary>
    public void Exit()
    {
        _exiting = true;
        Close();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    // Side pane: status groups ("filter:…") and labels ("label:…") all show the torrent table, filtered.
    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            Navigate(typeof(SettingsPage));
            return;
        }

        switch ((args.SelectedItem as NavigationViewItem)?.Tag as string)
        {
            case { } tag when tag.StartsWith("filter:", StringComparison.Ordinal):
                _torrents.ShowFilter(Enum.Parse<TorrentFilter>(tag["filter:".Length..]));
                break;
            case { } tag when tag.StartsWith("label:", StringComparison.Ordinal):
                _torrents.ShowLabel(tag["label:".Length..]);
                break;
            default:
                return;
        }

        Navigate(typeof(TorrentsPage));
    }

    private void Navigate(Type page)
    {
        if (ContentFrame.CurrentSourcePageType != page)
        {
            ContentFrame.Navigate(page, null, new EntranceNavigationTransitionInfo());
            ContentFrame.BackStack.Clear();
        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e) =>
        SearchBox.Visibility = e.Content is ISearchablePage ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Keeps one side-pane item per label, in the labels' order, after the "Labels" header.</summary>
    private void SyncLabelItems()
    {
        int header = NavView.MenuItems.IndexOf(LabelsHeader);
        var existing = NavView.MenuItems.Skip(header + 1).OfType<NavigationViewItem>().ToList();
        foreach (NavigationViewItem item in existing)
        {
            if (item.Tag is not string tag || !_torrents.Labels.Any(l => "label:" + l.Name == tag))
            {
                if (ReferenceEquals(NavView.SelectedItem, item))
                {
                    NavView.SelectedItem = NavAll;
                }

                NavView.MenuItems.Remove(item);
            }
        }

        if (_torrents.Labels.Count == 0)
        {
            NavView.MenuItems.Add(new NavigationViewItem
            {
                Content = new TextBlock { Text = Strings.Get("NavNoLabels"), Style = (Style)Application.Current.Resources["TertiaryCaptionStyle"], TextWrapping = TextWrapping.Wrap },
                SelectsOnInvoked = false,
                IsTabStop = false,
                Tag = "hint",
            });
            return;
        }

        for (int i = 0; i < _torrents.Labels.Count; i++)
        {
            LabelItem label = _torrents.Labels[i];
            string tag = "label:" + label.Name;
            var item = NavView.MenuItems.Skip(header + 1).OfType<NavigationViewItem>().FirstOrDefault(n => (string?)n.Tag == tag)
                ?? CreateLabelItem(label);
            int index = header + 1 + i;
            int current = NavView.MenuItems.IndexOf(item);
            if (current != index)
            {
                if (current >= 0)
                {
                    NavView.MenuItems.RemoveAt(current);
                }

                NavView.MenuItems.Insert(Math.Min(index, NavView.MenuItems.Count), item);
            }
        }
    }

    private NavigationViewItem CreateLabelItem(LabelItem label)
    {
        var count = new TextBlock { Style = (Style)Application.Current.Resources["TertiaryCaptionStyle"] };
        count.SetBinding(TextBlock.TextProperty, new Microsoft.UI.Xaml.Data.Binding { Source = label, Path = new PropertyPath(nameof(LabelItem.Count)) });
        var content = new Grid { ColumnSpacing = 8 };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.Children.Add(new TextBlock { Text = label.Name, TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(count, 1);
        content.Children.Add(count);

        var item = new NavigationViewItem { Content = content, Icon = Icons.HugeIcons.Create(Icons.HugeIconKind.Tag01), Tag = "label:" + label.Name };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, label.Name);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(item, "NavLabel_" + label.Name);

        var delete = new MenuFlyoutItem { Text = Strings.Get("Menu_DeleteLabel"), Icon = Icons.HugeIcons.Create(Icons.HugeIconKind.Delete02) };
        delete.Click += (_, _) => _torrents.DeleteLabel(label.Name);
        var menu = new MenuFlyout();
        menu.Items.Add(delete);
        item.ContextFlyout = menu;
        return item;
    }

    /// <summary>The search box is as wide as Mail's (440), shrinking in narrow windows so the title stays.</summary>
    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e) =>
        SearchBox.Width = Math.Clamp(e.NewSize.Width - 420, 200, 440);

    private void AppTitleBar_PaneToggleRequested(TitleBar sender, object args) => NavView.IsPaneOpen = !NavView.IsPaneOpen;

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => ApplySearch();

    private void ApplySearch()
    {
        if (ContentFrame.Content is ISearchablePage page)
        {
            page.ApplySearch(SearchBox.Text);
        }
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Esc clears the search, a second Esc leaves the box.
        if (e.Key == VirtualKey.Escape && SearchBox.Text.Length > 0)
        {
            SearchBox.Text = "";
            e.Handled = true;
        }
    }

    private void FocusSearch_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (SearchBox.Visibility == Visibility.Visible)
        {
            SearchBox.Focus(FocusState.Keyboard);
            args.Handled = true;
        }
    }

    /// <summary>Caption buttons follow the window's theme, not the system's.</summary>
    private void UpdateCaptionButtons()
    {
        AppWindowTitleBar bar = AppWindow.TitleBar;
        if (HighContrast.IsOn)
        {
            bar.ButtonForegroundColor = bar.ButtonHoverForegroundColor = bar.ButtonPressedForegroundColor = null;
            bar.ButtonInactiveForegroundColor = bar.ButtonBackgroundColor = bar.ButtonInactiveBackgroundColor = null;
            bar.ButtonHoverBackgroundColor = bar.ButtonPressedBackgroundColor = null;
            return;
        }

        Color ink = RootGrid.ActualTheme == ElementTheme.Dark ? Colors.White : Colors.Black;
        Color Tint(byte alpha) => Color.FromArgb(alpha, ink.R, ink.G, ink.B);
        bar.ButtonForegroundColor = bar.ButtonHoverForegroundColor = bar.ButtonPressedForegroundColor = ink;
        bar.ButtonInactiveForegroundColor = Tint(0x5C);
        bar.ButtonBackgroundColor = bar.ButtonInactiveBackgroundColor = Colors.Transparent;
        bar.ButtonHoverBackgroundColor = Tint(0x0F);
        bar.ButtonPressedBackgroundColor = Tint(0x0A);
    }

    private void SetupTray()
    {
        try
        {
            _tray = new TrayIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), "Wintor");
        }
        catch (Exception)
        {
            _tray = null; // no notification area (e.g. some kiosk setups): closing just quits
            return;
        }

        _tray.MenuItems.Add((Strings.Get("Tray_Open"), ShowAndActivate));
        _tray.MenuItems.Add((Strings.Get("Tray_ResumeAll"), () => _ = _torrents.ResumeAllAsync()));
        _tray.MenuItems.Add((Strings.Get("Tray_PauseAll"), () => _ = _torrents.PauseAllAsync()));
        _tray.MenuItems.Add((null, null));
        _tray.MenuItems.Add((Strings.Get("Tray_Exit"), Exit));
        _tray.Activated += ShowAndActivate;
        _tray.SessionEnding += Exit;
        _torrents.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TorrentsViewModel.TotalSpeedText))
            {
                _tray?.SetTooltip(string.IsNullOrEmpty(_torrents.TotalSpeedText) ? "Wintor" : "Wintor\n" + _torrents.TotalSpeedText);
            }
        };
    }

    private void OnTorrentCompleted(TorrentSnapshot torrent)
    {
        if (_settings.NotifyWhenComplete)
        {
            _notifications.ShowCompleted(torrent.Name, torrent.ContentPath);
        }
    }

    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_engineStopped)
        {
            return;
        }

        // Closing the window keeps seeding in the notification area, unless the user asked to quit.
        if (!_exiting && _settings.CloseToTray && _tray is { IsShown: true })
        {
            args.Cancel = true;
            AppWindow.Hide();
            if (!_settings.TrayHintShown)
            {
                _settings.TrayHintShown = true;
                _settings.Save();
                _notifications.ShowStillRunning();
            }

            return;
        }

        // Stop the engine gracefully so resume data is written, then close for real.
        args.Cancel = true;
        AppWindow.Hide();
        try
        {
            await App.Services.GetRequiredService<ITorrentEngine>().DisposeAsync();
        }
        finally
        {
            _engineStopped = true;
            _tray?.Dispose();
            _notifications.Unregister();
            Close();
        }
    }
}
