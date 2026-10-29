# Captures the README / Microsoft Store screenshots from "wintor --demo": sample torrents (legal content only), English,
# its own settings, the real Wintor's window size. Runs through UI Automation: it never moves the mouse or types. The
# menu shots are taken from the screen, so the demo window comes to the front for them. Needs Wintor installed
# (winapp run src\Wintor.App) and Windows PowerShell 5.1:
#   powershell -ExecutionPolicy Bypass -File tools\screenshots.ps1
param([string]$Out = (Join-Path $PSScriptRoot '..\docs\screenshots'))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null

function Start-Demo([string]$Theme) {
    $before = @(Get-Process Wintor -ErrorAction SilentlyContinue | ForEach-Object Id)
    Start-Process wintor -ArgumentList "--demo --$Theme"
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 250
        $demo = Get-Process Wintor -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id } | Select-Object -First 1
        if ($demo) { break }
    }
    if (-not $demo) { throw 'The demo did not start.' }
    Start-Sleep -Seconds 4   # let the table fill and the speeds settle
    $demo.Id
}

function Stop-Demo([int]$DemoPid) {
    Start-Process wintor -ArgumentList '--demo --quit'
    for ($i = 0; $i -lt 40 -and (Get-Process -Id $DemoPid -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 250 }
}

function Ui([int]$DemoPid, [string[]]$Arguments) {
    $output = & winapp ui @Arguments -a $DemoPid 2>&1
    if ($LASTEXITCODE -ne 0) { throw "winapp ui $($Arguments -join ' ') failed: $output" }
    Plain $output
}

# winapp colors its output even when it is redirected.
function Plain($Lines) { $Lines | ForEach-Object { "$_" -replace "$([char]27)\[[0-9;]*m", '' } }

function Select-FirstTorrent([int]$DemoPid) {
    $row = (Ui $DemoPid @('inspect', 'TorrentList', '--depth', '2') | Select-String '^\s*(\S+) ListItem' | Select-Object -First 1).Matches.Groups[1].Value
    Ui $DemoPid @('invoke', $row) | Out-Null
    # Selecting through UI Automation leaves a keyboard focus frame on the row; a tab takes the focus away.
    Ui $DemoPid @('invoke', 'TabFiles') | Out-Null
    Ui $DemoPid @('invoke', 'TabGeneral') | Out-Null
    Start-Sleep -Milliseconds 800
}

function Get-MainWindow([int]$DemoPid) {
    (& winapp ui list-windows --json -a $DemoPid 2>$null | ConvertFrom-Json | ForEach-Object { $_ } |
        Where-Object title -eq 'Wintor' | Select-Object -First 1).hwnd
}

# The newest popup (menus are popup windows of their own, a submenu opens last).
function Get-Popup([int]$DemoPid) {
    $popup = @(& winapp ui list-windows --json -a $DemoPid 2>$null | ConvertFrom-Json | ForEach-Object { $_ } |
        Where-Object title -eq 'PopupHost') | Select-Object -Last 1
    if (-not $popup) { throw 'The menu did not open.' }
    [string]$popup.hwnd
}

# Only the window's client area: a capture by app or window would add a caption and owned popups (tooltips).
# -WithMenus captures that area from the screen instead, with the open menus in place and their shadows.
function Shot([int]$DemoPid, [string]$Name, [switch]$WithMenus) {
    $window = Get-MainWindow $DemoPid
    $client = (Plain (& winapp ui inspect -w $window --depth 1 2>$null) | Select-String '^\s+(pn-[0-9a-f]+) Pane \(' |
        Select-Object -First 1).Matches.Groups[1].Value
    [string[]]$screen = if ($WithMenus) { '--capture-screen' } else { @() }
    $output = & winapp ui screenshot $client -w $window @screen -o (Join-Path $Out "$Name.png") 2>&1
    if ($LASTEXITCODE -ne 0) { throw "screenshot $Name failed: $output" }
    "  $Name.png"
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class DemoWindow {
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
    // Moves the window, keeping its size, so that its bottom edge is at the given screen line.
    public static void MoveBottomTo(long window, int bottom) {
        Rect r;
        GetWindowRect(new IntPtr(window), out r);
        SetWindowPos(new IntPtr(window), IntPtr.Zero, r.Left, bottom - (r.Bottom - r.Top), 0, 0, 0x15); // NOSIZE | NOZORDER | NOACTIVATE
    }
}
'@

# "When done" opens to the right of its item at the bottom of the side pane and runs past the window, onto the screen
# below or off it. With the window at the bottom of the screen, the menu is pushed up into the window.
function Move-ToScreenBottom([int]$DemoPid) {
    [DemoWindow]::MoveBottomTo([long](Get-MainWindow $DemoPid), [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea.Bottom)
    Start-Sleep -Milliseconds 300
}

# The row menu is a context flyout, which UI Automation opens with IUIAutomationElement3.ShowContextMenu. That
# interface is not in System.Windows.Automation, so the UIAutomationClient type library is converted once.
function Show-RowMenu([int]$DemoPid, [string]$Name) {
    if (-not ('RowMenu' -as [type])) {
        $dir = Join-Path $env:TEMP 'wintor-screenshots'
        New-Item -ItemType Directory -Force $dir | Out-Null
        $interop = Join-Path $dir 'Interop.UIAutomationClient.dll'
        if (-not (Test-Path $interop)) {
            Add-Type @'
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
public class TypeLibSink : ITypeLibImporterNotifySink {
    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    static extern void LoadTypeLibEx(string file, int kind, out ITypeLib library);
    public void ReportEvent(ImporterEventKind kind, int code, string message) { }
    public Assembly ResolveRef(object library) { return null; }
    public static void Convert(string source, string path) {
        ITypeLib library;
        LoadTypeLibEx(source, 2, out library); // REGKIND_NONE
        var assembly = (System.Reflection.Emit.AssemblyBuilder)new TypeLibConverter().ConvertTypeLibToAssembly(
            library, path, TypeLibImporterFlags.None, new TypeLibSink(), null, null, "Interop.UIAutomationClient", null);
        assembly.Save(System.IO.Path.GetFileName(path));
    }
}
'@
            [TypeLibSink]::Convert("$env:windir\System32\UIAutomationCore.dll", $interop)
        }
        Add-Type -Path $interop
        Add-Type -ReferencedAssemblies $interop @'
using System;
using Interop.UIAutomationClient;
public static class RowMenu {
    public static void Show(long window, string name) {
        var automation = new CUIAutomation8();
        var rows = automation.ElementFromHandle(new IntPtr(window)).FindAll(TreeScope.TreeScope_Descendants,
            automation.CreatePropertyCondition(30003, 50007)); // ControlType = ListItem
        for (int i = 0; i < rows.Length; i++) {
            var row = rows.GetElement(i);
            if (row.CurrentName != null && row.CurrentName.StartsWith(name)) { ((IUIAutomationElement3)row).ShowContextMenu(); return; }
        }
        throw new InvalidOperationException("No row " + name);
    }
}
'@
    }
    [RowMenu]::Show([long](Get-MainWindow $DemoPid), $Name)
    Start-Sleep -Milliseconds 600
}

function Close-Menus([int]$DemoPid, [int]$Levels) {
    for ($i = 0; $i -lt $Levels; $i++) {
        Ui $DemoPid @('send-keys', 'escape', '-w', (Get-MainWindow $DemoPid)) | Out-Null
        Start-Sleep -Milliseconds 300
    }
}

foreach ($theme in 'light', 'dark') {
    "$theme theme:"
    $demo = Start-Demo $theme
    try {
        Shot $demo "torrents-$theme"
        Select-FirstTorrent $demo
        Shot $demo "details-$theme"
        Ui $demo @('invoke', 'TabSpeed') | Out-Null
        Start-Sleep -Milliseconds 800
        Shot $demo "speed-$theme"
        Ui $demo @('invoke', 'TabPeers') | Out-Null
        Start-Sleep -Milliseconds 800
        Shot $demo "peers-$theme"
        Ui $demo @('invoke', 'SettingsItem') | Out-Null
        Start-Sleep -Milliseconds 800
        Shot $demo "settings-$theme"

        # The menus last: closing them with Escape turns on keyboard focus frames.
        Ui $demo @('invoke', 'NavTorrents') | Out-Null
        Start-Sleep -Milliseconds 1500
        Move-ToScreenBottom $demo
        # A video lower in the list: its menu has Play and opens above the row without leaving the window.
        Ui $demo @('invoke', 'Tears of Steel') | Out-Null
        Ui $demo @('invoke', 'TabGeneral') | Out-Null
        Start-Sleep -Milliseconds 800
        Show-RowMenu $demo 'Tears of Steel'
        Ui $demo @('invoke', 'Menu_DownloadOrder', '-w', (Get-Popup $demo)) | Out-Null
        Start-Sleep -Milliseconds 600
        Shot $demo "row-menu-$theme" -WithMenus
        Close-Menus $demo 2
        Ui $demo @('invoke', 'NavActions') | Out-Null
        Start-Sleep -Milliseconds 600
        Ui $demo @('invoke', 'WhenDoneMenu', '-w', (Get-Popup $demo)) | Out-Null
        Start-Sleep -Milliseconds 600
        Shot $demo "when-done-$theme" -WithMenus
        Close-Menus $demo 2
    }
    finally {
        Stop-Demo $demo
    }
}
