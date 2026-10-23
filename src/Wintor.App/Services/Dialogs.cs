using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Wintor.App.Services;

/// <summary>
/// How every content dialog in Wintor is shown (rules: docs/DESIGN.md, "Dialogs"). WinUI allows one open
/// <see cref="ContentDialog"/> per window and throws for a second, so dialogs queue; the template is adjusted so the
/// primary (accent) button sits on the right of the footer band. Adapted from the author's Mail app.
/// </summary>
public static class Dialogs
{
    private static readonly TimeSpan OpenDialogPoll = TimeSpan.FromMilliseconds(150);

    /// <summary>One gate per window's XamlRoot; forgotten with the window.</summary>
    private static readonly ConditionalWeakTable<XamlRoot, SemaphoreSlim> Gates = new();

    /// <summary>Dialogs already set up by <see cref="AdjustLayout"/>.</summary>
    private static readonly ConditionalWeakTable<ContentDialog, object> Adjusted = new();

    /// <summary>
    /// Readies <paramref name="dialog"/> for the window of <paramref name="root"/>: its XamlRoot, the window's theme
    /// (dialogs live in a popup layer that does not inherit the root element's RequestedTheme) and the Fluent style
    /// (a ContentDialog subclass does not pick up the app's implicit style, and the old one has no footer band).
    /// </summary>
    public static T Prepare<T>(T dialog, XamlRoot root) where T : ContentDialog
    {
        dialog.XamlRoot = root;
        if (root.Content is FrameworkElement content)
        {
            dialog.RequestedTheme = content.ActualTheme;
        }

        if (dialog.Style is null && Application.Current.Resources.TryGetValue("DefaultContentDialogStyle", out object? style))
        {
            dialog.Style = (Style)style;
        }

        return dialog;
    }

    /// <summary>
    /// Shows <paramref name="dialog"/> after any dialog open in its window has closed. Call on the UI thread after
    /// <see cref="Prepare"/>. Never throws: a dialog that cannot be shown answers <see cref="ContentDialogResult.None"/>.
    /// </summary>
    public static async Task<ContentDialogResult> ShowQueuedAsync(this ContentDialog dialog)
    {
        XamlRoot? root = dialog.XamlRoot;
        SemaphoreSlim? gate = root is null ? null : Gates.GetValue(root, _ => new SemaphoreSlim(1, 1));
        try
        {
            if (gate is not null)
            {
                await gate.WaitAsync();
            }

            try
            {
                // A dialog shown some other way is still open: wait for it too.
                while (root is not null && HasOpenDialog(root))
                {
                    await Task.Delay(OpenDialogPoll);
                }

                AdjustLayout(dialog);
                return await dialog.ShowAsync();
            }
            finally
            {
                gate?.Release();
            }
        }
        catch (Exception)
        {
            return ContentDialogResult.None;
        }
    }

    /// <summary>
    /// The primary button deletes: red (Tailwind red-600, red-700 under the pointer, red-800 pressed, white text in
    /// both themes) instead of the accent, and it takes the focus when the dialog opens, so Enter deletes. Set
    /// <see cref="ContentDialog.DefaultButton"/> to Primary. The reds are this button's meaning, not theme chrome.
    /// </summary>
    public static void MakeDestructive(this ContentDialog dialog)
    {
        var text = new SolidColorBrush(Microsoft.UI.Colors.White);
        var rest = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xDC, 0x26, 0x26));
        var over = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xB9, 0x1C, 0x1C));
        var pressed = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x99, 0x1B, 0x1B));
        // Lightweight styling: the primary button's AccentButtonStyle looks these up from the dialog first.
        dialog.Resources["AccentButtonBackground"] = rest;
        dialog.Resources["AccentButtonBackgroundPointerOver"] = over;
        dialog.Resources["AccentButtonBackgroundPressed"] = pressed;
        dialog.Resources["AccentButtonBorderBrush"] = rest;
        dialog.Resources["AccentButtonBorderBrushPointerOver"] = over;
        dialog.Resources["AccentButtonBorderBrushPressed"] = pressed;
        dialog.Resources["AccentButtonForeground"] = text;
        dialog.Resources["AccentButtonForegroundPointerOver"] = text;
        dialog.Resources["AccentButtonForegroundPressed"] = text;
        dialog.DefaultButton = ContentDialogButton.Primary;
        dialog.Opened += (_, _) =>
        {
            if (FindDescendant<Button>(dialog, b => b.Name == "PrimaryButton") is { } delete)
            {
                delete.Focus(FocusState.Programmatic);
            }
        };
    }

    /// <summary>
    /// Mirrors the footer band so the primary (accent) button is on the right and Cancel on the left; when the
    /// content is a <see cref="ScrollViewer"/>, the template's bottom padding moves onto the scrolled content, so it
    /// scrolls right up to the footer. The mouse's Back button closes the dialog, as Esc does.
    /// </summary>
    private static void AdjustLayout(ContentDialog dialog)
    {
        if (Adjusted.TryGetValue(dialog, out _))
        {
            return;
        }

        Adjusted.Add(dialog, new object());

        void Apply()
        {
            // Primary | Secondary | Close → Close | Secondary | Primary; the buttons themselves stay left-to-right.
            if (FindDescendant<Grid>(dialog, g => g.Name == "CommandSpace") is { } commands)
            {
                commands.FlowDirection = FlowDirection.RightToLeft;
                foreach (FrameworkElement button in commands.Children.OfType<FrameworkElement>())
                {
                    button.FlowDirection = FlowDirection.LeftToRight;
                }
            }

            if (dialog.Content is ScrollViewer { Content: FrameworkElement inner }
                && FindDescendant<ContentPresenter>(dialog, p => p.Name == "Content") is { } presenter
                && VisualTreeHelper.GetParent(presenter) is Grid padded)
            {
                Thickness gap = padded.Padding;
                padded.Padding = new Thickness(gap.Left, gap.Top, gap.Right, 0);
                inner.Margin = new Thickness(inner.Margin.Left, inner.Margin.Top, inner.Margin.Right, gap.Bottom);
            }
        }

        // The template parts may not exist yet when Opened is raised, so on Loaded too.
        dialog.Loaded += (_, _) => Apply();
        dialog.Opened += (_, _) => Apply();
        dialog.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (e.Pointer.PointerDeviceType == Microsoft.UI.Input.PointerDeviceType.Mouse
                && e.GetCurrentPoint(dialog).Properties.IsXButton1Pressed)
            {
                e.Handled = true;
                dialog.Hide();
            }
        }), handledEventsToo: true);
    }

    private static bool HasOpenDialog(XamlRoot root)
    {
        try
        {
            return VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Any(p => p.IsOpen && p.Child is ContentDialog);
        }
        catch (Exception)
        {
            return false; // the window is closing: ShowAsync reports it
        }
    }

    /// <summary>The first descendant of type <typeparamref name="T"/> that <paramref name="match"/> accepts, depth first.</summary>
    private static T? FindDescendant<T>(DependencyObject parent, Func<T, bool> match) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found && match(found))
            {
                return found;
            }

            if (FindDescendant(child, match) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }
}
