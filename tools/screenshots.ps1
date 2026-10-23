# Captures the README / Microsoft Store screenshots from "wintor --demo": sample torrents (legal content only), English,
# its own settings, the real Wintor's window size. Runs in the background through UI Automation: it never moves the
# mouse or types. Needs Wintor installed (winapp run src\Wintor.App) and Windows PowerShell 5.1:
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
    $output
}

function Select-FirstTorrent([int]$DemoPid) {
    $row = (Ui $DemoPid @('inspect', 'TorrentList', '--depth', '2') | Select-String '^\s*(\S+) ListItem' | Select-Object -First 1).Matches.Groups[1].Value
    Ui $DemoPid @('invoke', $row) | Out-Null
    # Selecting through UI Automation leaves a keyboard focus frame on the row; a tab takes the focus away.
    Ui $DemoPid @('invoke', 'TabFiles') | Out-Null
    Ui $DemoPid @('invoke', 'TabGeneral') | Out-Null
    Start-Sleep -Milliseconds 800
}

# Only the window's client area: a capture by app or window would add a caption and owned popups (tooltips).
function Shot([int]$DemoPid, [string]$Name) {
    $window = (& winapp ui list-windows --json -a $DemoPid 2>$null | ConvertFrom-Json | ForEach-Object { $_ } |
        Where-Object title -eq 'Wintor' | Select-Object -First 1).hwnd
    $client = (& winapp ui inspect -w $window --depth 1 2>$null | Select-String '^\s+(pn-[0-9a-f]+) Pane \(' |
        Select-Object -First 1).Matches.Groups[1].Value
    $output = & winapp ui screenshot $client -w $window -o (Join-Path $Out "$Name.png") 2>&1
    if ($LASTEXITCODE -ne 0) { throw "screenshot $Name failed: $output" }
    "  $Name.png"
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
    }
    finally {
        Stop-Demo $demo
    }
}
