# UI smoke tests driven through UI Automation (winapp ui).
#
# Uses only UIA patterns (invoke, set-value, wait-for) and window capture, so it does not move
# the mouse or steal focus — you can keep working while it runs. Things UIA cannot judge
# (hover, drag-and-drop, animations, real look and feel) go to manual-checks.md instead.
#
#   .\tests\ui\ui-tests.ps1 -Target sandbox   # preferred: isolated guest, may add/remove torrents
#   .\tests\ui\ui-tests.ps1 -Target local     # this machine: read-only flows, never adds torrents
#
# Results: tests\ui\artifacts\<run>\results.json plus screenshots.

param(
    [Parameter(Mandatory)][ValidateSet('sandbox', 'local')][string]$Target,
    [int]$AppPid = 0,
    [ValidateSet('sandbox', 'local')][string]$AppScope,
    [switch]$NoBuild,
    [ValidateNotNullOrEmpty()][string]$WorkflowId = [guid]::NewGuid().ToString('N'),
    [string]$ArtifactDirectory = (Join-Path $PSScriptRoot "artifacts\$(Get-Date -Format 'yyyyMMdd-HHmmss')")
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$AppProject = Join-Path $PSScriptRoot '..\..\src\Wintor.App'
$ScopeArgs = @(if ($Target -eq 'sandbox') { '--on'; 'sandbox' })
$previousWorkflowId = $env:WINAPP_UI_WORKFLOW_ID
$env:WINAPP_UI_WORKFLOW_ID = $WorkflowId
$results = [Collections.Generic.List[object]]::new()
$screenshots = [Collections.Generic.List[string]]::new()
$uiStarted = $false

# A legal, well-seeded test torrent (Big Buck Bunny). Only ever added inside the sandbox.
$TestMagnet = 'magnet:?xt=urn:btih:dd8255ecdc7ca55fb0bbf81323d87062db1f6d1c&dn=Big+Buck+Bunny'

function Invoke-WinAppChecked {
    param([string[]]$Arguments)
    $global:LASTEXITCODE = $null
    $output = @(& winapp @Arguments)
    $code = $global:LASTEXITCODE
    if ($null -eq $code -or $code -ne 0) {
        throw "winapp $($Arguments -join ' ') failed (exit $code). $($output -join "`n")"
    }
    $output
}

function Invoke-Ui {
    param([string[]]$Arguments, [string]$Window)
    $selector = if ($Window) { @('-w', $Window) } else { @('-a', "$AppPid") }
    Invoke-WinAppChecked (@('ui') + $Arguments + $ScopeArgs + $selector)
}

function Test-UI {
    param([string]$Name, [scriptblock]$Script)
    try {
        & $Script | Out-Null
        $results.Add(@{ name = $Name; status = 'PASS' })
    } catch {
        $results.Add(@{ name = $Name; status = 'FAIL'; detail = "$_" })
    }
}

function Save-Screenshot {
    param([string]$Name)
    $path = Join-Path $ArtifactDirectory $Name
    if (Test-Path -LiteralPath $path) { throw "Choose a new evidence path: $path" }
    # No --focus / --capture-screen: those bring the window to the foreground.
    Invoke-Ui @('screenshot', '--output', $path) -Window $hwnd | Out-Null
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-Item -LiteralPath $path).Length -eq 0) {
        throw "Screenshot was not delivered to the host: $path"
    }
    $screenshots.Add($path)
}

# ContentDialog buttons are template parts; their AutomationIds come from the template.
function Close-Dialog { Invoke-Ui @('invoke', 'CloseButton'); Invoke-Ui @('wait-for', 'CloseButton', '--gone', '-t', '3000') }

try {
    $ArtifactDirectory = [IO.Path]::GetFullPath($ArtifactDirectory)
    New-Item -ItemType Directory -Force -Path $ArtifactDirectory | Out-Null
    if ($AppPid -lt 0) { throw 'AppPid must be positive, or zero to launch.' }
    if ($AppPid -gt 0) {
        if ($AppScope -ne $Target) {
            throw 'Reusing a PID requires matching -AppScope and -Target from the same live target.'
        }
    } else {
        if ($Target -eq 'local' -and (Get-Process Wintor -ErrorAction SilentlyContinue)) {
            # The app is single-instance: a second launch would hand off to the running copy.
            throw 'Wintor is already running on this machine. Close it, or pass -AppPid <pid> -AppScope local.'
        }
        $runArgs = @('run', $AppProject, '--arch', 'x64') + $ScopeArgs + @('--detach', '--json')
        if ($NoBuild) { $runArgs += '--no-build' }
        $launch = Invoke-WinAppChecked $runArgs | ConvertFrom-Json
        if ($launch.Error -or -not $launch.ProcessId -or [int]$launch.ProcessId -le 0) {
            throw "No app ProcessId. $($launch.Error)"
        }
        $AppPid = [int]$launch.ProcessId
        if ($Target -eq 'sandbox' -and
            ($launch.ProcessScope -ne 'sandbox' -or $launch.UiTargetArgs -ne "--on sandbox -a $AppPid")) {
            throw 'Launch response is missing the expected Sandbox ProcessScope / UiTargetArgs pair.'
        }
        if ($Target -eq 'local' -and $launch.ProcessScope -and $launch.ProcessScope -ne 'local') {
            throw 'Launch response belongs to a different target.'
        }
    }

    $uiStarted = $true
    Invoke-Ui @('wait-for', 'NavTorrents', '-t', '15000') | Out-Null
    # Windows PowerShell 5.1 emits a JSON array as one object: unroll it so each window is filtered on its own.
    $windows = @(Invoke-Ui @('list-windows', '--json') | ConvertFrom-Json | ForEach-Object { $_ })
    $main = $windows | Where-Object { $_.hwnd -and $_.hwnd -ne '0' -and $_.title -ne 'PopupHost' } |
        Select-Object -First 1
    if (-not $main) { throw 'No app window found in the selected target.' }
    $hwnd = [string]$main.hwnd

    # --- Torrents page -------------------------------------------------------------------
    Test-UI 'Torrents page loads with commands' {
        Invoke-Ui @('wait-for', 'AddMagnetButton', '-t', '5000')
        Invoke-Ui @('wait-for', 'OpenFileButton', '-t', '2000')
    }
    Test-UI 'Torrents page screenshot' { Save-Screenshot '01-torrents.png' }
    Test-UI 'Remove is reachable (directly or via the overflow menu)' {
        $inOverflow = $false
        try { Invoke-Ui @('wait-for', 'RemoveButton', '-t', '1000') | Out-Null } catch {
            Invoke-Ui @('invoke', 'MoreButton') | Out-Null
            Invoke-Ui @('wait-for', 'RemoveButton', '-t', '2000') | Out-Null
            $inOverflow = $true
        }
        $empty = $true
        try { Invoke-Ui @('wait-for', 'EmptyTitle', '-t', '500') | Out-Null } catch { $empty = $false }
        if ($empty) {
            # Nothing to select on an empty list, so Remove must be disabled.
            Invoke-Ui @('wait-for', 'RemoveButton', '-p', 'IsEnabled', '--value', 'False', '-t', '2000') | Out-Null
        }
        if ($inOverflow) { Invoke-Ui @('invoke', 'MoreButton') | Out-Null }
    }

    $hasTorrents = $true
    try { Invoke-Ui @('wait-for', 'EmptyTitle', '-t', '500') | Out-Null; $hasTorrents = $false } catch { }

    # --- Side pane, table and search (read-only) -----------------------------------------
    Test-UI 'Status filters are in the side pane' {
        foreach ($id in 'NavTorrents', 'NavDownloading', 'NavCompleted', 'NavPaused') {
            Invoke-Ui @('wait-for', $id, '-t', '2000') -Window $hwnd | Out-Null
        }
    }
    Test-UI 'Side pane filter switches the view' {
        Invoke-Ui @('invoke', 'NavCompleted') -Window $hwnd | Out-Null
        Start-Sleep -Milliseconds 400
        Save-Screenshot '01c-completed-view.png'
        Invoke-Ui @('invoke', 'NavTorrents') -Window $hwnd | Out-Null
        Invoke-Ui @('wait-for', 'TorrentsSummary', '-t', '2000') -Window $hwnd | Out-Null
    }
    Test-UI 'Column headers sort the table' {
        foreach ($id in 'HeaderName', 'HeaderSize', 'HeaderProgress', 'HeaderDownloadSpeed') {
            Invoke-Ui @('wait-for', $id, '-t', '2000') -Window $hwnd | Out-Null
        }
        Invoke-Ui @('invoke', 'HeaderSize') -Window $hwnd | Out-Null
        Start-Sleep -Milliseconds 300
        # Back to the user's own order (№, ascending), which the app remembers.
        Invoke-Ui @('invoke', 'HeaderQueue') -Window $hwnd | Out-Null
    }
    Test-UI 'Search box is in the title bar' { Invoke-Ui @('wait-for', 'SearchBox', '-t', '2000') }
    if ($hasTorrents) {
        Test-UI 'Search with no match shows "nothing found", clearing it brings the list back' {
            # The AutoSuggestBox takes text through its inner template part, "TextBox".
            Invoke-Ui @('set-value', 'TextBox', 'zz-no-such-torrent-zz') -Window $hwnd
            Invoke-Ui @('wait-for', 'NothingFound', '-t', '3000') -Window $hwnd
            Save-Screenshot '01b-nothing-found.png'
            # PowerShell 5.1 drops empty native arguments; the app trims the query, so a space clears it.
            Invoke-Ui @('set-value', 'TextBox', ' ') -Window $hwnd
            Invoke-Ui @('wait-for', 'NothingFound', '--gone', '-t', '3000') -Window $hwnd
        }

        # --- Details pane (read-only: selects a row and looks) -----------------------------
        Test-UI 'Selecting a row shows its details in the pane below' {
            Invoke-Ui @('wait-for', 'DetailsNothingSelected', '-t', '2000') -Window $hwnd | Out-Null
            # Row slugs come from the row's text, so find the first one now; invoking selects it.
            $row = (Invoke-Ui @('inspect', 'TorrentList', '--depth', '2') -Window $hwnd |
                Select-String '^\s*(\S+) ListItem' | Select-Object -First 1).Matches.Groups[1].Value
            if (-not $row) { throw 'No torrent row found in the table.' }
            Invoke-Ui @('invoke', $row) -Window $hwnd | Out-Null
            Invoke-Ui @('wait-for', 'DownloadedLabel', '-t', '3000') -Window $hwnd | Out-Null
            Invoke-Ui @('wait-for', 'SectionTransfer', '-t', '1000') -Window $hwnd | Out-Null
            Start-Sleep -Milliseconds 1500
            Save-Screenshot '06-details-general.png'
        }
        Test-UI 'Details tabs: files, peers, trackers, speed' {
            foreach ($tab in @(@('TabFiles', 'FilesList'), @('TabTrackers', 'TrackersList'), @('TabSpeed', 'SpeedLegend'))) {
                Invoke-Ui @('invoke', $tab[0]) -Window $hwnd | Out-Null
                Invoke-Ui @('wait-for', $tab[1], '-t', '3000') -Window $hwnd | Out-Null
            }
            Start-Sleep -Milliseconds 500
            Save-Screenshot '07-details-speed.png'
            Invoke-Ui @('invoke', 'TabGeneral') -Window $hwnd | Out-Null
        }
    }

    # --- Add magnet dialog (validation only, nothing is added) ---------------------------
    Test-UI 'Magnet dialog opens' {
        Invoke-Ui @('invoke', 'AddMagnetButton')
        Invoke-Ui @('wait-for', 'MagnetInput', '-t', '3000')
    }
    Test-UI 'Magnet dialog rejects invalid input' {
        Invoke-Ui @('set-value', 'MagnetInput', 'not a magnet link')
        Invoke-Ui @('wait-for', 'PrimaryButton', '-p', 'IsEnabled', '--value', 'False', '-t', '2000')
    }
    Test-UI 'Magnet dialog accepts a magnet link' {
        Invoke-Ui @('set-value', 'MagnetInput', $TestMagnet)
        Invoke-Ui @('wait-for', 'PrimaryButton', '-p', 'IsEnabled', '--value', 'True', '-t', '2000')
    }
    Test-UI 'Magnet dialog screenshot' { Save-Screenshot '02-magnet-dialog.png' }

    if ($Target -eq 'sandbox') {
        # --- Add and remove a torrent (sandbox only: changes app state and downloads) ----
        Test-UI 'Adding a magnet shows it in the list' {
            Invoke-Ui @('invoke', 'PrimaryButton')
            Invoke-Ui @('wait-for', 'TorrentList', '-t', '10000')
            Invoke-Ui @('wait-for', 'Big Buck Bunny', '-t', '30000')
        }
        Test-UI 'Torrent list screenshot' { Save-Screenshot '03-torrent-added.png' }
        Test-UI 'Remove asks for confirmation and removes' {
            Invoke-Ui @('invoke', 'Big Buck Bunny')
            Invoke-Ui @('wait-for', 'RemoveButton', '-p', 'IsEnabled', '--value', 'True', '-t', '3000')
            Invoke-Ui @('invoke', 'RemoveButton')
            Invoke-Ui @('wait-for', 'DeleteFilesCheckBox', '-t', '3000')
            Save-Screenshot '04-remove-dialog.png'
            Invoke-Ui @('invoke', 'PrimaryButton')
            Invoke-Ui @('wait-for', 'EmptyTitle', '-t', '10000')
        }
    } else {
        Test-UI 'Magnet dialog cancels' { Close-Dialog }
    }

    # --- Settings ------------------------------------------------------------------------
    Test-UI 'Settings page opens' {
        Invoke-Ui @('invoke', 'SettingsItem')
        Invoke-Ui @('wait-for', 'SettingsTitle', '-t', '3000')
        Invoke-Ui @('wait-for', 'ChangeFolderButton', '-t', '2000')
    }
    Test-UI 'Settings has appearance, speed, connection and Windows sections' {
        foreach ($id in 'ThemeBox', 'BackdropBox', 'NotifySwitch', 'DownloadLimitBox', 'UploadLimitBox', 'PortBox', 'StartupSwitch', 'TraySwitch') {
            Invoke-Ui @('wait-for', $id, '-t', '2000') | Out-Null
        }
    }
    Test-UI 'About expander opens' {
        # The expander's toggle is a template part without a stable id: take the first button inside.
        $toggle = (Invoke-Ui @('inspect', 'AboutCard', '--depth', '2') |
            Select-String '^\s*(\S+) Button' | Select-Object -First 1).Matches.Groups[1].Value
        if (-not $toggle) { throw 'About expander toggle not found.' }
        Invoke-Ui @('invoke', $toggle)
        Invoke-Ui @('wait-for', $toggle, '-p', 'ExpandCollapseState', '--value', 'Expanded', '-t', '2000')
        Start-Sleep -Milliseconds 400
        Save-Screenshot '05-settings.png'
    }
    Test-UI 'Back to torrents' {
        Invoke-Ui @('invoke', 'NavTorrents')
        Invoke-Ui @('wait-for', 'AddMagnetButton', '-t', '3000')
    }

    # --- Accessibility -------------------------------------------------------------------
    Test-UI 'App controls have AutomationIds' {
        $inspection = Invoke-Ui @('inspect', '--interactive', '--json') -Window $hwnd | ConvertFrom-Json
        function Get-Elements($nodes) {
            foreach ($node in $nodes) {
                if ($null -ne $node) { $node; Get-Elements $node.children }
            }
        }
        $allElements = @(Get-Elements @($inspection.windows | ForEach-Object { $_.elements }))
        if (-not $allElements.Count) { throw 'Inspection returned no elements; not an accessibility PASS.' }
        $appElements = @($allElements | Where-Object {
            $_.type -match 'Button|TextBox|ComboBox|CheckBox|ToggleSwitch|TabItem|Edit' -and
            $_.name -notmatch 'Minimize|Maximize|Close|System' -and
            $_.className -notmatch 'PickerHost|#32770|CabinetWClass'
        })
        if (-not $appElements.Count) { throw 'No app controls were audited; adjust selectors rather than passing.' }
        $missingId = @($appElements | Where-Object { -not $_.automationId })
        if ($missingId.Count) {
            throw "Missing AutomationIds: $(($missingId | ForEach-Object { "$($_.type) '$($_.name)'" }) -join ', ')"
        }
    }
} catch {
    $results.Add(@{ name = 'Target setup / test execution'; status = 'FAIL'; detail = "$_" })
} finally {
    try {
        if ($uiStarted) { Invoke-WinAppChecked (@('ui', 'yield') + $ScopeArgs) | Out-Null }
    } catch {
        $results.Add(@{ name = 'Release UI workflow'; status = 'FAIL'; detail = "$_" })
    } finally {
        $env:WINAPP_UI_WORKFLOW_ID = $previousWorkflowId
    }
}

$failed = @($results | Where-Object { $_.status -eq 'FAIL' }).Count
$report = [ordered]@{
    target = $Target; appPid = $AppPid; workflowId = $WorkflowId
    passed = $results.Count - $failed; failed = $failed
    results = @($results.ToArray()); screenshots = @($screenshots.ToArray())
}
$resultsPath = Join-Path $ArtifactDirectory 'results.json'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resultsPath -Encoding utf8
Write-Host "Passed: $($report.passed) | Failed: $failed | Results: $resultsPath"
if ($failed) { exit 1 }
exit 0
