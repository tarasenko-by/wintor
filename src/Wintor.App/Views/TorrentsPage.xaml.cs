using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Wintor.App.Icons;
using Wintor.App.Services;
using Wintor.App.ViewModels;
using Wintor.Core;

namespace Wintor.App.Views;

public sealed partial class TorrentsPage : Page, ISearchablePage
{
    private const double DefaultPaneHeight = 300;
    private const double MinPaneHeight = 140;

    private readonly AppSettings _settings = App.Services.GetRequiredService<AppSettings>();
    private readonly MenuFlyout _itemMenu = new();
    private bool _showingDialog;
    private double _paneHeightAtDragStart;

    public TorrentsPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
        _itemMenu.Opening += ItemMenu_Opening;
        ViewModel.AddRequested += () => DispatcherQueue.TryEnqueue(() => _ = ShowPendingAddsAsync());
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        PaneRow.Height = new GridLength(Math.Max(_settings.DetailsPaneHeight, MinPaneHeight));
        _settings.DetailsPaneHeight = PaneRow.Height.Value;
        Splitter.DragStarted += (_, _) => _paneHeightAtDragStart = PaneRow.ActualHeight;
        Splitter.DragDelta += (_, offset) => SetPaneHeight(_paneHeightAtDragStart - offset);
        Splitter.DragCompleted += (_, _) => SavePaneHeight();
        Splitter.ResetRequested += (_, _) =>
        {
            SetPaneHeight(DefaultPaneHeight);
            SavePaneHeight();
        };

        TableColumns.Current.Attach(_settings);
        // Measured on the header row itself (minus its 12 px padding each side), the same width the rows lay out in.
        HeaderGrid.SizeChanged += (_, e) => TableColumns.Current.Fit(e.NewSize.Width - 24);
        BuildHeaders();
        TableColumns.Current.PropertyChanged += (_, _) => UpdateHeaderVisibility();
        ColumnsButton.Flyout = CreateColumnsMenu();
        HeaderGrid.ContextFlyout = CreateColumnsMenu();
        UpdateHeaders();
    }

    /// <summary>Header cells in table order: column key (as in <see cref="TableColumns"/>) and what it sorts by.</summary>
    private static readonly (string Column, TorrentSort Sort, bool RightAligned)[] HeaderColumns =
    [
        ("Queue", TorrentSort.Queue, true),
        ("Name", TorrentSort.Name, false),
        ("Size", TorrentSort.Size, true),
        ("Progress", TorrentSort.Progress, false),
        ("Status", TorrentSort.Status, false),
        ("Down", TorrentSort.DownloadSpeed, true),
        ("Up", TorrentSort.UploadSpeed, true),
        ("TimeLeft", TorrentSort.TimeLeft, true),
        ("Seeds", TorrentSort.Seeds, true),
        ("Peers", TorrentSort.Peers, true),
    ];

    private readonly Dictionary<TorrentSort, Button> _headerButtons = [];
    private readonly Dictionary<string, FrameworkElement> _headerCells = [];
    private readonly Dictionary<string, Controls.ColumnResizer> _resizers = [];

    public TorrentsViewModel ViewModel { get; } = App.Services.GetRequiredService<TorrentsViewModel>();

    public static bool HasText(string? text) => !string.IsNullOrEmpty(text);

    public string SearchPlaceholder => Strings.Get("Search_Torrents");

    public void ApplySearch(string text) => ViewModel.Search = text.Trim();

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
        await ShowPendingAddsAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TorrentsViewModel.Sort) or nameof(TorrentsViewModel.SortDescending))
        {
            UpdateHeaders();
        }
    }

    // --- Table layout ---

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Keep the table usable when the window gets shorter; grow back to the saved height when it gets taller.
        SetPaneHeight(_settings.DetailsPaneHeight, e.NewSize.Height);
    }

    private void SetPaneHeight(double height, double? pageHeight = null)
    {
        double available = pageHeight ?? LayoutRoot.ActualHeight;
        if (available <= 0)
        {
            return; // not laid out yet; SizeChanged comes with the real height
        }

        // Leave the header, the table's column titles and a few rows above the pane.
        double max = Math.Max(MinPaneHeight, available - 280);
        PaneRow.Height = new GridLength(Math.Clamp(height, MinPaneHeight, max));
    }

    private void SavePaneHeight()
    {
        _settings.DetailsPaneHeight = PaneRow.ActualHeight;
        _settings.Save();
    }

    /// <summary>One cell per column: a quiet button that sorts, and (except for the name) a width handle.</summary>
    private void BuildHeaders()
    {
        for (int i = 0; i < HeaderColumns.Length; i++)
        {
            var (column, sort, right) = HeaderColumns[i];
            var button = new Button
            {
                Style = (Style)Resources[right ? "ColumnHeaderButtonRightStyle" : "ColumnHeaderButtonStyle"],
                Tag = sort.ToString(),
            };
            AutomationProperties.SetAutomationId(button, "Header" + sort);
            button.Click += Header_Click;
            _headerButtons[sort] = button;

            var cell = new Grid();
            cell.Children.Add(button);

            // The divider on this cell's right edge sits between this column and the next one shown: dragging it
            // trades width between just those two, so it follows the pointer and nothing else moves.
            var resizer = new Controls.ColumnResizer();
            AutomationProperties.SetAutomationId(resizer, "Resize" + column);
            string? neighbour = null;
            double leftStart = 0, rightStart = 0;
            resizer.DragStarted += (_, _) =>
            {
                neighbour = TableColumns.Current.NextShown(column);
                leftStart = TableColumns.Current.CurrentWidth(column);
                rightStart = neighbour is null ? 0 : TableColumns.Current.CurrentWidth(neighbour);
            };
            resizer.DragDelta += (_, offset) =>
            {
                if (neighbour is not null)
                {
                    TableColumns.Current.ResizeBetween(column, neighbour, leftStart, rightStart, offset);
                }
            };
            resizer.DragCompleted += (_, _) => TableColumns.Current.SaveWidths();
            resizer.ResetRequested += (_, _) =>
            {
                TableColumns.Current.ResetWidth(column);
                if (TableColumns.Current.NextShown(column) is { } next)
                {
                    TableColumns.Current.ResetWidth(next);
                }
            };
            cell.Children.Add(resizer);
            _resizers[column] = resizer;

            Grid.SetColumn(cell, i);
            HeaderGrid.Children.Add(cell);
            _headerCells[column] = cell;
        }

        UpdateHeaderVisibility();
    }

    /// <summary>A column at width 0 (turned off or squeezed out) must not leave its handle in the gap.</summary>
    private void UpdateHeaderVisibility()
    {
        foreach (var (column, cell) in _headerCells)
        {
            cell.Visibility = TableColumns.Current.IsShownNow(column) ? Visibility.Visible : Visibility.Collapsed;
        }

        // The last column has nobody to trade width with, so no divider after it.
        foreach (var (column, resizer) in _resizers)
        {
            resizer.Visibility = TableColumns.Current.NextShown(column) is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    /// <summary>Which columns show: right-click on the header or the button at its end. Locked ones stay ticked.</summary>
    private MenuFlyout CreateColumnsMenu()
    {
        var menu = new MenuFlyout { Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedRight };
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            var name = new ToggleMenuFlyoutItem { Text = Strings.Get("Column_Name"), IsChecked = true, IsEnabled = false };
            menu.Items.Add(name);
            foreach (ColumnInfo column in TableColumns.All)
            {
                TorrentSort sort = HeaderColumns.First(h => h.Column == column.Key).Sort;
                var item = new ToggleMenuFlyoutItem
                {
                    Text = Strings.Get("Column_" + sort),
                    IsChecked = TableColumns.Current.IsChosen(column.Key),
                    IsEnabled = !column.Locked,
                };
                AutomationProperties.SetAutomationId(item, "ColumnToggle" + column.Key);
                item.Click += (_, _) => TableColumns.Current.SetChosen(column.Key, item.IsChecked);
                menu.Items.Add(item);
            }

            menu.Items.Add(new MenuFlyoutSeparator());
            var reset = new MenuFlyoutItem { Text = Strings.Get("Columns_Reset") };
            AutomationProperties.SetAutomationId(reset, "ColumnsReset");
            reset.Click += (_, _) => TableColumns.Current.Reset();
            menu.Items.Add(reset);
        };
        return menu;
    }

    /// <summary>Column titles, with an arrow on the column the table is sorted by.</summary>
    private void UpdateHeaders()
    {
        foreach (var (column, header) in _headerButtons)
        {
            string title = Strings.Get("Column_" + column);
            bool active = ViewModel.Sort == column;
            var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            content.Children.Add(new TextBlock { Text = title, TextTrimming = TextTrimming.CharacterEllipsis });
            if (active)
            {
                content.Children.Add(HugeIcons.Create(ViewModel.SortDescending ? HugeIconKind.ArrowDown01 : HugeIconKind.ArrowUp01, 12));
            }

            header.Content = content;
            ToolTipService.SetToolTip(header, Strings.Format("Column_SortBy", title));
            AutomationProperties.SetName(header, active
                ? Strings.Format(ViewModel.SortDescending ? "Column_SortedDescending" : "Column_SortedAscending", title)
                : title);
        }
    }

    private void Header_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag } && Enum.TryParse(tag, out TorrentSort column))
        {
            ViewModel.SortBy(column);
        }
    }

    private void SortMenu_Opening(object? sender, object e)
    {
        SortMenu.Items.Clear();
        foreach (TorrentSort column in Enum.GetValues<TorrentSort>())
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = Strings.Get("Column_" + column),
                GroupName = "column",
                IsChecked = ViewModel.Sort == column,
            };
            item.Click += (_, _) => ViewModel.Sort = column;
            SortMenu.Items.Add(item);
        }

        SortMenu.Items.Add(new MenuFlyoutSeparator());
        foreach (bool descending in new[] { false, true })
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = Strings.Get(descending ? "Sort_Descending" : "Sort_Ascending"),
                GroupName = "direction",
                IsChecked = ViewModel.SortDescending == descending,
            };
            item.Click += (_, _) => ViewModel.SortDescending = descending;
            SortMenu.Items.Add(item);
        }
    }

    // --- Adding ---

    private async void AddMagnet_Click(object sender, RoutedEventArgs e)
    {
        string? prefill = null;
        try
        {
            // Most people copy the link right before adding it — prefill from the clipboard.
            DataPackageView clipboard = Clipboard.GetContent();
            if (clipboard.Contains(StandardDataFormats.Text))
            {
                string text = await clipboard.GetTextAsync();
                prefill = TorrentInput.IsMagnetLink(text) ? text.Trim() : null;
            }
        }
        catch (Exception)
        {
            // The clipboard can be locked by another app; prefilling is only a convenience.
        }

        await AddMagnetWithDialogAsync(prefill);
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e) =>
        await AddTorrentFilesAsync(await Shell.PickTorrentFilesAsync());

    /// <summary>One file gets the dialog (folder, files); several are added straight to the default folder.</summary>
    private async Task AddTorrentFilesAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 1)
        {
            await AddTorrentFileWithDialogAsync(paths[0]);
        }
        else
        {
            foreach (string path in paths)
            {
                await ViewModel.AddTorrentFileAsync(path);
            }
        }
    }

    private async Task AddMagnetWithDialogAsync(string? magnetLink)
    {
        if (_showingDialog)
        {
            return;
        }

        _showingDialog = true;
        try
        {
            AddTorrentDialog dialog = Dialogs.Prepare(AddTorrentDialog.ForMagnet(ViewModel.DownloadDirectory, magnetLink), XamlRoot);
            if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.AddMagnetAsync(dialog.MagnetLink, dialog.Folder);
            }
        }
        finally
        {
            _showingDialog = false;
        }
    }

    private async Task AddTorrentFileWithDialogAsync(string path)
    {
        if (_showingDialog)
        {
            return;
        }

        TorrentMetadata metadata;
        try
        {
            metadata = await ViewModel.ReadTorrentFileAsync(path);
        }
        catch (Exception)
        {
            ViewModel.ErrorMessage = Strings.Format("Error_BadTorrentFile", Path.GetFileName(path));
            return;
        }

        _showingDialog = true;
        try
        {
            AddTorrentDialog dialog = Dialogs.Prepare(AddTorrentDialog.ForTorrentFile(ViewModel.DownloadDirectory, metadata), XamlRoot);
            if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.AddTorrentFileAsync(path, dialog.Folder, dialog.SkippedFiles);
            }
        }
        finally
        {
            _showingDialog = false;
        }
    }

    /// <summary>Adds that came from a .torrent double-click or a magnet link in a browser, one dialog at a time.</summary>
    private async Task ShowPendingAddsAsync()
    {
        if (XamlRoot is null)
        {
            return; // the page is not on screen yet; OnNavigatedTo calls again
        }

        while (!_showingDialog && ViewModel.TryTakePendingAdd(out AddRequest request))
        {
            if (request.Kind == AddRequestKind.Magnet)
            {
                await AddMagnetWithDialogAsync(request.Value);
            }
            else
            {
                await AddTorrentFileWithDialogAsync(request.Value);
            }
        }
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems) || e.DataView.Contains(StandardDataFormats.Text))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = Strings.Get("Drop_Caption");
        }
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
            await AddTorrentFilesAsync(items.Select(i => i.Path).Where(TorrentInput.IsTorrentFile).ToList());
        }
        else if (e.DataView.Contains(StandardDataFormats.Text))
        {
            string text = await e.DataView.GetTextAsync();
            if (TorrentInput.IsMagnetLink(text))
            {
                await AddMagnetWithDialogAsync(text.Trim());
            }
        }
    }

    // --- Selection and actions ---

    private void TorrentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        List<TorrentItemViewModel> selection = TorrentList.SelectedItems.OfType<TorrentItemViewModel>().ToList();
        ViewModel.SetSelection(selection);
        _ = DetailsPane.ViewModel.ShowAsync(selection.FirstOrDefault()?.Id);
    }

    private void TorrentList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args) =>
        args.ItemContainer.ContextFlyout ??= _itemMenu;

    private void TorrentList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        bool alt = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Menu)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (alt && e.Key is VirtualKey.Up or VirtualKey.Down && ViewModel.HasSelection)
        {
            ViewModel.Move(ViewModel.SelectedTorrents, e.Key == VirtualKey.Up ? -1 : 1);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape && TorrentList.SelectedItems.Count > 0)
        {
            TorrentList.SelectedItems.Clear();
            e.Handled = true;
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e) => _ = ConfirmRemoveAsync(ViewModel.SelectedTorrents);

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        foreach (TorrentItemViewModel item in ViewModel.SelectedTorrents.Take(5))
        {
            Shell.OpenFolder(item.ContentPath);
        }
    }

    private void CopyMagnet_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedTorrents.FirstOrDefault() is { } item)
        {
            _ = CopyMagnetAsync(item.Id);
        }
    }

    private void ResumeAll_Click(object sender, RoutedEventArgs e) => _ = ViewModel.ResumeAllAsync();

    private void PauseAll_Click(object sender, RoutedEventArgs e) => _ = ViewModel.PauseAllAsync();

    private void SelectAll_Click(object sender, RoutedEventArgs e) => TorrentList.SelectAll();

    private async Task ConfirmRemoveAsync(IReadOnlyList<TorrentItemViewModel> items)
    {
        if (items.Count == 0 || _showingDialog)
        {
            return;
        }

        var deleteFiles = new CheckBox { Content = Strings.Get("RemoveDialog_DeleteFiles") };
        AutomationProperties.SetAutomationId(deleteFiles, "DeleteFilesCheckBox");
        string what = items.Count == 1 ? items[0].Name : Strings.Format("RemoveDialog_Several", items.Count);
        var dialog = Dialogs.Prepare(new ContentDialog
        {
            Title = Strings.Get(items.Count == 1 ? "RemoveDialog_Title" : "RemoveDialog_TitleSeveral"),
            Content = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = what, TextWrapping = TextWrapping.Wrap },
                    deleteFiles,
                },
            },
            PrimaryButtonText = Strings.Get("RemoveDialog_Remove"),
            CloseButtonText = Strings.Get("Cancel"),
        }, XamlRoot);
        dialog.MakeDestructive();

        _showingDialog = true;
        try
        {
            if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
            {
                await ViewModel.RemoveAsync(items, deleteFiles.IsChecked == true);
            }
        }
        finally
        {
            _showingDialog = false;
        }
    }

    private async Task<string?> AskLabelNameAsync()
    {
        if (_showingDialog)
        {
            return null;
        }

        var input = new TextBox { Header = Strings.Get("LabelDialog_Header"), MaxLength = 40 };
        AutomationProperties.SetAutomationId(input, "LabelNameInput");
        var dialog = Dialogs.Prepare(new ContentDialog
        {
            Title = Strings.Get("LabelDialog_Title"),
            Content = input,
            PrimaryButtonText = Strings.Get("LabelDialog_Create"),
            CloseButtonText = Strings.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
        }, XamlRoot);
        input.TextChanged += (_, _) => dialog.IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(input.Text);

        _showingDialog = true;
        try
        {
            return await dialog.ShowQueuedAsync() == ContentDialogResult.Primary ? input.Text.Trim() : null;
        }
        finally
        {
            _showingDialog = false;
        }
    }

    /// <summary>
    /// Download and upload limits for the chosen torrents, in KB/s (0 = none of their own). Prefilled with their
    /// current limits when they all share them.
    /// </summary>
    private async Task AskSpeedLimitsAsync(IReadOnlyList<TorrentItemViewModel> targets)
    {
        if (targets.Count == 0 || _showingDialog)
        {
            return;
        }

        static double Shared(IEnumerable<int> limits)
        {
            var distinct = limits.Distinct().ToList();
            return distinct.Count == 1 ? distinct[0] / 1024 : 0;
        }

        NumberBox Box(string header, string id, double value)
        {
            var box = new NumberBox
            {
                Header = Strings.Get(header),
                Minimum = 0,
                Value = value,
                SmallChange = 128,
                LargeChange = 1024,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            };
            AutomationProperties.SetAutomationId(box, id);
            return box;
        }

        NumberBox down = Box("SpeedDialog_Download", "SpeedLimitDownload", Shared(targets.Select(t => t.Snapshot?.DownloadLimit ?? 0)));
        NumberBox up = Box("SpeedDialog_Upload", "SpeedLimitUpload", Shared(targets.Select(t => t.Snapshot?.UploadLimit ?? 0)));
        var dialog = Dialogs.Prepare(new ContentDialog
        {
            Title = Strings.Get("SpeedDialog_Title"),
            Content = new StackPanel
            {
                Spacing = 12,
                MinWidth = 360,
                Children =
                {
                    new TextBlock
                    {
                        Text = targets.Count == 1 ? targets[0].Name : Strings.Format("RemoveDialog_Several", targets.Count),
                        Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    },
                    down,
                    up,
                    new TextBlock
                    {
                        Text = Strings.Get("SpeedDialog_Hint"),
                        Style = (Style)Application.Current.Resources["SecondaryCaptionStyle"],
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
            },
            PrimaryButtonText = Strings.Get("SpeedDialog_Apply"),
            CloseButtonText = Strings.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        }, XamlRoot);

        _showingDialog = true;
        try
        {
            if (await dialog.ShowQueuedAsync() == ContentDialogResult.Primary)
            {
                // An empty box (NaN) means no limit.
                static int KiB(double value) => double.IsNaN(value) ? 0 : (int)Math.Clamp(value, 0, 10_000_000);
                await ViewModel.SetSpeedLimitsAsync(targets, KiB(down.Value), KiB(up.Value));
            }
        }
        finally
        {
            _showingDialog = false;
        }
    }

    // --- Context menu: one menu for every row, filled for the row it opens on ---

    private void ItemMenu_Opening(object? sender, object e)
    {
        _itemMenu.Items.Clear();
        if (_itemMenu.Target is not ListViewItem container || TorrentList.ItemFromContainer(container) is not TorrentItemViewModel item)
        {
            return;
        }

        // Right-clicking a row that is part of a multi-selection acts on the whole selection.
        IReadOnlyList<TorrentItemViewModel> targets = ViewModel.SelectedTorrents.Contains(item) ? ViewModel.SelectedTorrents : [item];

        if (targets.Any(t => !t.CanPause))
        {
            _itemMenu.Items.Add(Entry("Menu_Resume", HugeIconKind.Play, () => _ = ViewModel.ResumeAsync(targets)));
        }

        if (targets.Any(t => t.CanPause))
        {
            _itemMenu.Items.Add(Entry("Menu_Pause", HugeIconKind.Pause, () => _ = ViewModel.PauseAsync(targets)));
        }

        _itemMenu.Items.Add(Entry("Menu_OpenFolder", HugeIconKind.FolderOpen, () => Shell.OpenFolder(item.ContentPath)));
        if (targets.Count == 1)
        {
            _itemMenu.Items.Add(Entry("Menu_CopyMagnet", HugeIconKind.Link01, () => _ = CopyMagnetAsync(item.Id)));
        }

        _itemMenu.Items.Add(Entry("Menu_SpeedLimit", HugeIconKind.DashboardSpeed01, () => _ = AskSpeedLimitsAsync(targets)));
        _itemMenu.Items.Add(new MenuFlyoutSeparator());
        _itemMenu.Items.Add(LabelMenu(targets));
        _itemMenu.Items.Add(OrderMenu(targets));
        _itemMenu.Items.Add(new MenuFlyoutSeparator());
        _itemMenu.Items.Add(Entry("Menu_Remove", HugeIconKind.Delete02, () => _ = ConfirmRemoveAsync(targets)));
    }

    private MenuFlyoutSubItem LabelMenu(IReadOnlyList<TorrentItemViewModel> targets)
    {
        var menu = new MenuFlyoutSubItem { Text = Strings.Get("Menu_Label"), Icon = HugeIcons.Create(HugeIconKind.Tag01) };
        string? current = targets.Select(t => t.Label).Distinct().Count() == 1 ? targets[0].Label : null;
        foreach (LabelItem label in ViewModel.Labels)
        {
            // A check mark as accelerator text, as Mail does: ToggleMenuFlyoutItem would add a gutter to every entry.
            var entry = new MenuFlyoutItem
            {
                Text = label.Name,
                Icon = MainWindow.Swatch(label.Brush),
                KeyboardAcceleratorTextOverride = label.Name == current ? "✓" : "",
            };
            entry.Click += (_, _) => ViewModel.SetLabel(targets, label.Name);
            menu.Items.Add(entry);
        }

        if (ViewModel.Labels.Count > 0)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
        }

        menu.Items.Add(Entry("Menu_NewLabel", HugeIconKind.AddSquare, async () =>
        {
            if (await AskLabelNameAsync() is { } name)
            {
                ViewModel.SetLabel(targets, name);
            }
        }));
        if (targets.Any(t => t.HasLabel))
        {
            menu.Items.Add(Entry("Menu_NoLabel", HugeIconKind.Cancel01, () => ViewModel.SetLabel(targets, null)));
        }

        return menu;
    }

    private MenuFlyoutSubItem OrderMenu(IReadOnlyList<TorrentItemViewModel> targets)
    {
        var menu = new MenuFlyoutSubItem { Text = Strings.Get("Menu_Order"), Icon = HugeIcons.Create(HugeIconKind.Sorting01) };
        menu.Items.Add(Entry("Menu_MoveTop", HugeIconKind.ArrowUpDouble, () => ViewModel.Move(targets, int.MinValue)));
        menu.Items.Add(Entry("Menu_MoveUp", HugeIconKind.ArrowUp01, () => ViewModel.Move(targets, -1), "Alt+↑"));
        menu.Items.Add(Entry("Menu_MoveDown", HugeIconKind.ArrowDown01, () => ViewModel.Move(targets, 1), "Alt+↓"));
        menu.Items.Add(Entry("Menu_MoveBottom", HugeIconKind.ArrowDownDouble, () => ViewModel.Move(targets, int.MaxValue)));
        return menu;
    }

    private static MenuFlyoutItem Entry(string key, HugeIconKind icon, Action action, string? shortcut = null)
    {
        var entry = new MenuFlyoutItem { Text = Strings.Get(key), Icon = HugeIcons.Create(icon) };
        if (shortcut is not null)
        {
            entry.KeyboardAcceleratorTextOverride = shortcut;
        }

        entry.Click += (_, _) => action();
        return entry;
    }

    private async Task CopyMagnetAsync(string id)
    {
        TorrentDetails? details = await App.Services.GetRequiredService<ITorrentEngine>().GetDetailsAsync(id);
        if (details?.MagnetLink is { } link)
        {
            Shell.CopyText(link);
        }
    }
}
