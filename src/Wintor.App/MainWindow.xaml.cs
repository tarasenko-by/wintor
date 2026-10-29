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
    /// <summary>What the search box says while empty ("Search torrents", "Search settings").</summary>
    string SearchPlaceholder { get; }

    void ApplySearch(string text);
}

public sealed partial class MainWindow : Window
{
    private readonly AppSettings _settings = App.Services.GetRequiredService<AppSettings>();
    private readonly TorrentsViewModel _torrents = App.Services.GetRequiredService<TorrentsViewModel>();
    private readonly NotificationService _notifications = App.Services.GetRequiredService<NotificationService>();
    private readonly WhenDoneService _whenDone = App.Services.GetRequiredService<WhenDoneService>();
    private TrayIcon? _tray;
    private bool _exiting;
    private bool _restartAfterExit;
    private Action? _afterExit;
    private bool _shuttingDown;
    private bool _trayHintShown;
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

        RestorePlacement();
        AppWindow.Changed += (_, e) =>
        {
            if (e.DidPositionChange || e.DidSizeChange || e.DidPresenterChange)
            {
                RememberPlacement();
            }
        };

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
        _whenDone.RuleChanged += UpdateActionsItem;
        _whenDone.Triggered += OnWhenDone;
        SetupTray();

        SyncLabelItems();
        _torrents.Labels.CollectionChanged += (_, _) => SyncLabelItems();
        NavView.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, (_, _) => UpdateLabelsHeader());
        NavView.SelectedItem = NavAll;
        Activated += OfferWhatsNew;
    }

    /// <summary>
    /// After an update, "What's new" once, when the window is first really on screen (not when Wintor starts hidden
    /// in the tray with Windows). The version is stored as seen before the dialog opens, so quitting with it open does
    /// not bring it back.
    /// </summary>
    private void OfferWhatsNew(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated || !AppWindow.IsVisible)
        {
            return;
        }

        Activated -= OfferWhatsNew;
        Version current = WhatsNewDialog.CurrentVersion;
        WhatsNewStep step = WhatsNew.Decide(current, _settings.WhatsNewSeenVersion, _settings.ShowWhatsNew);
        if (step == WhatsNewStep.None)
        {
            return;
        }

        _settings.WhatsNewSeenVersion = WhatsNew.Stored(current);
        _settings.Save();
        if (step == WhatsNewStep.Show)
        {
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                async () => await WhatsNewDialog.ShowAsync(RootGrid.XamlRoot));
        }
    }

    /// <summary>
    /// Opens the window where the user left it. A saved place that is no longer on any screen (a monitor was
    /// unplugged) falls back to the default size on the main screen.
    /// </summary>
    private void RestorePlacement()
    {
        if (_settings.Window is { Width: > 0, Height: > 0 } saved)
        {
            var bounds = new RectInt32(saved.X, saved.Y, saved.Width, saved.Height);
            if (DisplayArea.GetFromRect(bounds, DisplayAreaFallback.None) is not null)
            {
                AppWindow.MoveAndResize(bounds);
                if (saved.Maximized && AppWindow.Presenter is OverlappedPresenter overlapped)
                {
                    overlapped.Maximize();
                }

                return;
            }
        }

        double scale = GetDpiForWindow(Win32Interop.GetWindowFromWindowId(AppWindow.Id)) / 96.0;
        AppWindow.Resize(new SizeInt32((int)(1280 * scale), (int)(800 * scale)));
    }

    /// <summary>
    /// Keeps the restored bounds while the user moves and resizes the window; saved to disk when Wintor closes or
    /// hides to the tray. A maximized or minimized window keeps the bounds it will return to.
    /// </summary>
    private void RememberPlacement()
    {
        if (!AppWindow.IsVisible || AppWindow.Presenter is not OverlappedPresenter overlapped
            || overlapped.State == OverlappedPresenterState.Minimized)
        {
            return;
        }

        bool maximized = overlapped.State == OverlappedPresenterState.Maximized;
        WindowPlacement? previous = _settings.Window;
        _settings.Window = maximized && previous is not null
            ? previous with { Maximized = true }
            : new WindowPlacement(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height, maximized);
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
        _ = ShutdownAsync();
    }

    /// <summary>Starts a fresh Wintor while this one is closing (the engine is already stopped).</summary>
    private static async Task LaunchNewInstanceAsync()
    {
        try
        {
            // Give up the single-instance key first: otherwise the new process finds it still held, hands its
            // launch to this one (already closing) and exits, and nothing comes back.
            Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().UnregisterKey();
            // AppInstance.Restart refuses when the window is not in the foreground, and ours is hidden by now;
            // launching our own Start menu entry has no such condition.
            var entries = await Windows.ApplicationModel.Package.Current.GetAppListEntriesAsync();
            await entries[0].LaunchAsync();
        }
        catch (Exception)
        {
            // Could not relaunch: Wintor simply closes, and the user starts it again.
        }
    }

    /// <summary>Quits gracefully like <see cref="Exit"/>, then starts again (e.g. to load another interface language).</summary>
    public void Restart()
    {
        _restartAfterExit = true;
        Exit();
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

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (ReferenceEquals(args.InvokedItemContainer, NavActions))
        {
            MenuFlyout menu = ActionsMenu();
            // Lit like on hover while the menu is open, so it is clear where the menu came from.
            menu.Opened += (_, _) => NavActions.Background = NavActions.Tag as Microsoft.UI.Xaml.Media.Brush;
            menu.Closed += (_, _) => NavActions.ClearValue(Control.BackgroundProperty);
            // Above the item, as the Start menu's power button does: the item sits at the bottom of the pane.
            menu.ShowAt(NavActions, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
            {
                Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.TopEdgeAlignedLeft,
            });
        }
    }

    /// <summary>
    /// The side pane's Actions: "When done" (as µTorrent's "Shut down Windows": Off, then each action after the
    /// downloads, or after the downloads and seeding), then the tray's Resume all / Pause all.
    /// </summary>
    private MenuFlyout ActionsMenu()
    {
        var whenDone = new MenuFlyoutSubItem { Text = Strings.Get("WhenDone_Menu"), Icon = Icons.HugeIcons.Create(Icons.HugeIconKind.ShutDown) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(whenDone, "WhenDoneMenu");
        whenDone.Items.Add(WhenDoneItem(WhenDoneRule.Off));
        foreach (WhenDoneAction action in Enum.GetValues<WhenDoneAction>())
        {
            if (action == WhenDoneAction.None
                || (action == WhenDoneAction.Sleep && !Power.CanSleep)
                || (action == WhenDoneAction.Hibernate && !Power.CanHibernate))
            {
                continue;
            }

            whenDone.Items.Add(new MenuFlyoutSeparator());
            whenDone.Items.Add(WhenDoneItem(new WhenDoneRule(action, WhenDoneCondition.DownloadsFinished)));
            whenDone.Items.Add(WhenDoneItem(new WhenDoneRule(action, WhenDoneCondition.AllFinished)));
        }

        var resume = new MenuFlyoutItem { Text = Strings.Get("Tray_ResumeAll"), Icon = Icons.HugeIcons.Create(Icons.HugeIconKind.Play) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(resume, "ActionsResumeAll");
        resume.Click += (_, _) => _ = _torrents.ResumeAllAsync();
        var pause = new MenuFlyoutItem { Text = Strings.Get("Tray_PauseAll"), Icon = Icons.HugeIcons.Create(Icons.HugeIconKind.Pause) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(pause, "ActionsPauseAll");
        pause.Click += (_, _) => _ = _torrents.PauseAllAsync();

        var menu = new MenuFlyout();
        menu.Items.Add(whenDone);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(resume);
        menu.Items.Add(pause);
        return menu;
    }

    private RadioMenuFlyoutItem WhenDoneItem(WhenDoneRule rule)
    {
        var item = new RadioMenuFlyoutItem
        {
            Text = WhenDoneText(rule),
            GroupName = "WhenDone",
            IsChecked = rule == _whenDone.Rule,
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(item, WhenDoneKey(rule));
        item.Click += (_, _) => _whenDone.Set(rule);
        return item;
    }

    private static string WhenDoneKey(WhenDoneRule rule) => rule.IsOff
        ? "WhenDone_Off"
        : $"WhenDone_{rule.Action}_{(rule.Condition == WhenDoneCondition.AllFinished ? "All" : "Downloads")}";

    private static string WhenDoneText(WhenDoneRule rule) => Strings.Get(WhenDoneKey(rule));

    /// <summary>While a "When done" action waits, Actions wears a dot and says which one in its tooltip.</summary>
    private void UpdateActionsItem()
    {
        WhenDoneRule rule = _whenDone.Rule;
        NavActions.InfoBadge = rule.IsOff ? null : new InfoBadge();
        ToolTipService.SetToolTip(NavActions, rule.IsOff ? null : WhenDoneText(rule));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(NavActions, rule.IsOff ? "" : WhenDoneText(rule));
    }

    /// <summary>The torrents are done: a minute's countdown in the window (brought back from the tray), then the action.</summary>
    private async void OnWhenDone(WhenDoneRule rule)
    {
        ShowAndActivate();
        if (!await WhenDoneDialog.ConfirmAsync(RootGrid.XamlRoot, rule))
        {
            return;
        }

        switch (rule.Action)
        {
            case WhenDoneAction.Quit:
                Exit();
                break;
            case WhenDoneAction.Sleep:
                _ = Task.Run(Power.Sleep); // returns only after the PC wakes up
                break;
            case WhenDoneAction.Hibernate:
                _ = Task.Run(Power.Hibernate);
                break;
            // Torrents stop first (resume data saved, as with "wintor --quit"), then Windows goes down.
            case WhenDoneAction.Restart:
                _afterExit = Power.Restart;
                Exit();
                break;
            case WhenDoneAction.ShutDown:
                _afterExit = Power.ShutDown;
                Exit();
                break;
        }
    }

    private void Navigate(Type page)
    {
        if (ContentFrame.CurrentSourcePageType != page)
        {
            ContentFrame.Navigate(page, null, new EntranceNavigationTransitionInfo());
            ContentFrame.BackStack.Clear();
        }
    }

    /// <summary>Each page searches its own content: the box takes the page's placeholder and starts empty.</summary>
    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        SearchHost.Visibility = e.Content is ISearchablePage ? Visibility.Visible : Visibility.Collapsed;
        if (e.Content is ISearchablePage page)
        {
            SearchBox.Text = "";
            SearchBox.PlaceholderText = page.SearchPlaceholder;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(SearchBox, page.SearchPlaceholder);
            page.ApplySearch("");
        }
    }

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
            UpdateLabelsHeader();
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

        UpdateLabelsHeader();
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

        // The tag icon wears the label's color.
        PathIcon icon = Icons.HugeIcons.Create(Icons.HugeIconKind.Tag01);
        icon.SetBinding(IconElement.ForegroundProperty, new Microsoft.UI.Xaml.Data.Binding { Source = label, Path = new PropertyPath(nameof(LabelItem.Brush)) });

        var item = new NavigationViewItem { Content = content, Icon = icon, Tag = "label:" + label.Name };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, label.Name);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(item, "NavLabel_" + label.Name);

        var menu = new MenuFlyout();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            var colors = new MenuFlyoutSubItem { Text = Strings.Get("Menu_LabelColor"), Icon = Swatch(label.Brush) };
            foreach (var (key, _) in LabelColors.All)
            {
                // A check mark as accelerator text: ToggleMenuFlyoutItem would add a gutter to every entry.
                var color = new MenuFlyoutItem
                {
                    Text = Strings.Get("LabelColor_" + key),
                    Icon = Swatch(LabelColors.BrushOf(key)),
                    KeyboardAcceleratorTextOverride = key == label.Color ? "✓" : "",
                };
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(color, "LabelColor_" + key);
                color.Click += (_, _) => _torrents.SetLabelColor(label.Name, key);
                colors.Items.Add(color);
            }

            var delete = new MenuFlyoutItem { Text = Strings.Get("Menu_DeleteLabel"), Icon = Icons.HugeIcons.Create(Icons.HugeIconKind.Delete02) };
            delete.Click += (_, _) => _torrents.DeleteLabel(label.Name);
            menu.Items.Add(colors);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(delete);
        };
        item.ContextFlyout = menu;
        return item;
    }

    /// <summary>A filled circle in a label's color, for menus.</summary>
    public static PathIcon Swatch(Microsoft.UI.Xaml.Media.Brush brush) => new()
    {
        Data = new Microsoft.UI.Xaml.Media.EllipseGeometry { Center = new Windows.Foundation.Point(8, 8), RadiusX = 6, RadiusY = 6 },
        Foreground = brush,
        Width = 16,
        Height = 16,
    };

    /// <summary>The "Labels" header and its hint need the open pane; the label icons alone fit the compact one.</summary>
    private void UpdateLabelsHeader()
    {
        Visibility open = NavView.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed;
        LabelsHeader.Visibility = open;
        foreach (NavigationViewItem hint in NavView.MenuItems.OfType<NavigationViewItem>().Where(i => (i.Tag as string) == "hint"))
        {
            hint.Visibility = open;
        }
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
        if (SearchHost.Visibility == Visibility.Visible)
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
            _settings.Save(); // the window's place (RememberPlacement)
            AppWindow.Hide();
            // Once per launch: enough to explain why the window "closed" but the app did not, without nagging.
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _notifications.ShowStillRunning();
            }

            return;
        }

        args.Cancel = true;
        await ShutdownAsync();
    }

    /// <summary>
    /// Stops the engine gracefully so resume data is written, then closes for real. Window.Close() alone does not
    /// raise AppWindow.Closing, so every way of quitting (×, the tray's Exit, "wintor --quit", restart) ends here.
    /// </summary>
    private async Task ShutdownAsync()
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;
        _settings.Save(); // the window's place (RememberPlacement)
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
            if (_restartAfterExit)
            {
                await LaunchNewInstanceAsync();
            }

            _afterExit?.Invoke();
            Close();
        }
    }
}
