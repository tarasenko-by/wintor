using System.Runtime.InteropServices;

namespace Wintor.App.Services;

/// <summary>
/// Notification-area (tray) icon on plain Win32: a hidden window receives the icon's callbacks and a native popup
/// menu offers commands. Must be created and used on the UI thread; WinUI's message loop pumps the hidden window.
/// </summary>
internal sealed partial class TrayIcon : IDisposable
{
    private const string WindowClassName = "Wintor.TrayIconWindow";
    private const uint WM_NULL = 0x0000;
    private const uint WM_CONTEXTMENU = 0x007B;
    private const uint WM_ENDSESSION = 0x0016;
    private const uint WM_SYSCOLORCHANGE = 0x0015;
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint WM_APP_TRAY = 0x8001;
    private const uint WM_TIMER = 0x0113;
    private const uint RetryEveryMs = 2000;
    private const int NIN_SELECT = 0x0400;
    private const int NIN_KEYSELECT = 0x0401;
    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_SHOWTIP = 0x80;
    private const uint NOTIFYICON_VERSION_4 = 4;
    private const uint MF_STRING = 0x0, MF_SEPARATOR = 0x800;
    private const uint TPM_RIGHTBUTTON = 0x2, TPM_RETURNCMD = 0x100, TPM_BOTTOMALIGN = 0x20;
    private const uint IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10;
    private const int SM_CXSMICON = 49, SM_CYSMICON = 50;
    private const uint SPI_GETHIGHCONTRAST = 0x0042, HCF_HIGHCONTRASTON = 0x1;
    private const int COLOR_WINDOW = 5;
    private static readonly UIntPtr RetryTimer = 1;

    private readonly WndProc _wndProc; // kept alive: native code holds a pointer to it
    private readonly IntPtr _hwnd;
    private IntPtr _icon;
    private string _iconPath;
    private readonly uint _taskbarCreatedMessage;
    private string _tooltip;
    private bool _disposed;

    public TrayIcon(string iconPath, string tooltip)
    {
        _tooltip = tooltip;
        _iconPath = iconPath;
        _wndProc = OnWindowMessage;

        var wc = new WNDCLASSEXW
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandleW(null),
            lpszClassName = WindowClassName,
        };
        RegisterClassExW(ref wc);

        // A regular hidden top-level window (not message-only) so it also gets the
        // "TaskbarCreated" broadcast and can re-add the icon after Explorer restarts.
        _hwnd = CreateWindowExW(0, WindowClassName, "", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, wc.hInstance, IntPtr.Zero);
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
        _icon = LoadIcon(iconPath);

        AddIcon();
    }

    /// <summary>
    /// Windows shows the icon. Adding fails while the taskbar is not ready (started at sign-in before Explorer);
    /// it is retried every few seconds, and until then the window has nowhere to hide.
    /// </summary>
    public bool IsShown { get; private set; }

    /// <summary>Left click / Enter on the icon.</summary>
    public event Action? Activated;

    /// <summary>Windows is ending the session or replacing the package: the app must exit now, not hide.</summary>
    public event Action? SessionEnding;

    /// <summary>The Windows mode or the system colors changed: <see cref="TaskbarIsLight"/> may say otherwise now.</summary>
    public event Action? SystemThemeChanged;

    /// <summary>
    /// The taskbar is light, so a monochrome icon has to be dark. It follows the Windows mode ("Choose your mode"),
    /// not the app's theme; in high contrast, the window color of the contrast theme.
    /// </summary>
    public static bool TaskbarIsLight
    {
        get
        {
            var contrast = new HIGHCONTRASTW { cbSize = (uint)Marshal.SizeOf<HIGHCONTRASTW>() };
            if (SystemParametersInfoW(SPI_GETHIGHCONTRAST, contrast.cbSize, ref contrast, 0) && (contrast.dwFlags & HCF_HIGHCONTRASTON) != 0)
            {
                uint c = GetSysColor(COLOR_WINDOW); // 0x00BBGGRR
                return (c & 0xFF) * 299 + ((c >> 8) & 0xFF) * 587 + ((c >> 16) & 0xFF) * 114 > 128_000;
            }

            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int light && light != 0;
        }
    }

    /// <summary>Context menu entries; a null text is a separator. The first entry is shown in bold.</summary>
    public List<(string? Text, Action? Action)> MenuItems { get; } = [];

    public void SetTooltip(string tooltip)
    {
        if (tooltip == _tooltip)
        {
            return;
        }

        _tooltip = tooltip;
        var data = NewData(NIF_TIP | NIF_SHOWTIP);
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    public void SetIcon(string iconPath)
    {
        if (iconPath == _iconPath)
        {
            return;
        }

        _iconPath = iconPath;
        IntPtr old = _icon;
        _icon = LoadIcon(iconPath);
        var data = NewData(NIF_ICON);
        Shell_NotifyIconW(NIM_MODIFY, ref data);
        if (old != IntPtr.Zero)
        {
            DestroyIcon(old);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        KillTimer(_hwnd, RetryTimer);
        var data = NewData(0);
        Shell_NotifyIconW(NIM_DELETE, ref data);
        if (_icon != IntPtr.Zero)
        {
            DestroyIcon(_icon);
        }

        DestroyWindow(_hwnd);
        UnregisterClassW(WindowClassName, GetModuleHandleW(null));
    }

    private static IntPtr LoadIcon(string path) =>
        LoadImageW(IntPtr.Zero, path, IMAGE_ICON, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), LR_LOADFROMFILE);

    private void AddIcon()
    {
        var data = NewData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        // After Explorer restarts the old icon is gone; when it is still there, adding fails and modifying works.
        IsShown = Shell_NotifyIconW(NIM_ADD, ref data) || Shell_NotifyIconW(NIM_MODIFY, ref data);
        if (!IsShown)
        {
            SetTimer(_hwnd, RetryTimer, RetryEveryMs, IntPtr.Zero);
            return;
        }

        KillTimer(_hwnd, RetryTimer);
        data.uVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, ref data);
    }

    private NOTIFYICONDATAW NewData(uint flags) => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _hwnd,
        uID = 1,
        uFlags = flags,
        uCallbackMessage = WM_APP_TRAY,
        hIcon = _icon,
        szTip = _tooltip.Length <= 127 ? _tooltip : _tooltip[..126] + "…",
        szInfo = "",
        szInfoTitle = "",
    };

    private IntPtr OnWindowMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // Called from native code: an exception escaping here would end the process.
        try
        {
            return HandleMessage(hwnd, msg, wParam, lParam);
        }
        catch (Exception)
        {
            return IntPtr.Zero;
        }
    }

    private IntPtr HandleMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_APP_TRAY)
        {
            // NOTIFYICON_VERSION_4: the event is in LOWORD(lParam), the cursor position in wParam.
            switch ((int)(lParam.ToInt64() & 0xFFFF))
            {
                case NIN_SELECT:
                case NIN_KEYSELECT:
                    Activated?.Invoke();
                    break;
                case (int)WM_CONTEXTMENU:
                    ShowMenu((short)(wParam.ToInt64() & 0xFFFF), (short)((wParam.ToInt64() >> 16) & 0xFFFF));
                    break;
            }

            return IntPtr.Zero;
        }

        // wParam is FALSE when the session end was cancelled after all.
        if (msg == WM_ENDSESSION)
        {
            if (wParam != IntPtr.Zero)
            {
                SessionEnding?.Invoke();
            }

            return IntPtr.Zero;
        }

        // Broadcast to every top-level window, hidden ones too: switching the Windows mode sends "ImmersiveColorSet",
        // a high-contrast theme also changes the system colors.
        if (msg is WM_SETTINGCHANGE or WM_SYSCOLORCHANGE)
        {
            SystemThemeChanged?.Invoke();
            return DefWindowProcW(hwnd, msg, wParam, lParam);
        }

        if ((msg == _taskbarCreatedMessage && _taskbarCreatedMessage != 0) || (msg == WM_TIMER && wParam == (IntPtr)RetryTimer))
        {
            AddIcon();
            return IntPtr.Zero;
        }

        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    private void ShowMenu(int x, int y)
    {
        IntPtr menu = CreatePopupMenu();
        for (int i = 0; i < MenuItems.Count; i++)
        {
            string? text = MenuItems[i].Text;
            if (text is null)
            {
                AppendMenuW(menu, MF_SEPARATOR, UIntPtr.Zero, null);
            }
            else
            {
                AppendMenuW(menu, MF_STRING, (UIntPtr)(i + 1), text);
            }
        }

        SetMenuDefaultItem(menu, 1, 0);

        // Required so the menu closes when the user clicks elsewhere.
        SetForegroundWindow(_hwnd);
        int command = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, x, y, _hwnd, IntPtr.Zero);
        PostMessageW(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);

        if (command > 0 && command <= MenuItems.Count)
        {
            MenuItems[command - 1].Action?.Invoke();
        }
    }

    private delegate IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATAW
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion; // union with uTimeout
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct HIGHCONTRASTW
    {
        public uint cbSize;
        public uint dwFlags;
        public IntPtr lpszDefaultScheme;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool UnregisterClassW(string className, IntPtr instance);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProcW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessageW(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandleW(string? name);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIconW(uint message, ref NOTIFYICONDATAW data);
    [DllImport("user32.dll")] private static extern UIntPtr SetTimer(IntPtr hwnd, UIntPtr id, uint ms, IntPtr callback);
    [DllImport("user32.dll")] private static extern bool KillTimer(IntPtr hwnd, UIntPtr id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadImageW(IntPtr instance, string name, uint type, int cx, int cy, uint load);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern uint GetSysColor(int index);
    [DllImport("user32.dll")] private static extern bool SystemParametersInfoW(uint action, uint param, ref HIGHCONTRASTW value, uint winIni);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenuW(IntPtr menu, uint flags, UIntPtr id, string? text);
    [DllImport("user32.dll")] private static extern bool SetMenuDefaultItem(IntPtr menu, uint item, uint byPosition);
    [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr tpm);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool PostMessageW(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
}
