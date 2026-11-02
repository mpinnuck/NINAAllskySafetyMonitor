<#
.SYNOPSIS
    Checks the NINA logs for Allsky Safety Monitor messages and makes sure no secrets were logged.

.DESCRIPTION
    Searches every NINA log written since -Since (default: today). It shows the plugin's
    messages, counts the warnings and errors, and checks that none of the given secrets (passwords,
    API keys) appear anywhere in the NINA logs or the plugin's decision logs (REQ-54). Lines
    containing a secret are never printed, only counted per file.

    With -Exceptions it also prints every exception in those logs that involves the plugin,
    with its full stack trace (the indented "at ..." lines that follow it).

.EXAMPLE
    .\Check-NinaLog.ps1 -Secret test123,5356e369

.EXAMPLE
    .\Check-NinaLog.ps1 -Since (Get-Date).AddDays(-2) -Tail 100

.EXAMPLE
    .\Check-NinaLog.ps1 -Exceptions -NoSecrets
#>
[CmdletBinding()]
param(
    [string]   $LogFolder = (Join-Path $env:LOCALAPPDATA 'NINA\Logs'),
    [string]   $DecisionFolder = (Join-Path $env:LOCALAPPDATA 'NINAAllskySafetyMonitor\Logs'),
    [datetime] $Since     = (Get-Date).Date,
    [string[]] $Secret,
    [int]      $Tail      = 60,
    [switch]   $Exceptions,
    [switch]   $NoSecrets
)

$tag = '[AllskySafetyMonitor]'

$files = @(Get-ChildItem -Path $LogFolder -Filter *.log -Recurse -ErrorAction Stop |
           Where-Object { $_.LastWriteTime -ge $Since } |
           Sort-Object LastWriteTime)

if ($files.Count -eq 0) {
    Write-Warning "No NINA logs in $LogFolder written since $Since."
    return
}

Write-Host "Logs since $($Since.ToString('yyyy-MM-dd HH:mm')):" -ForegroundColor Cyan
$files | ForEach-Object { Write-Host "  $($_.Name)" }

# ---- Plugin messages ----
$lines = @($files | Select-String -Pattern $tag -SimpleMatch)
Write-Host ""
Write-Host "Plugin messages: $($lines.Count) (showing the last $Tail)" -ForegroundColor Cyan
$lines | Select-Object -Last $Tail | ForEach-Object {
    # NINA format: time|LEVEL|file|method|line|message
    $parts = $_.Line -split '\|', 6
    if ($parts.Count -eq 6) {
        $colour = switch ($parts[1]) { 'ERROR' { 'Red' } 'WARNING' { 'Yellow' } default { 'Gray' } }
        Write-Host ("{0}  {1,-7} {2}" -f $parts[0], $parts[1], $parts[5].Replace("$tag ", '')) -ForegroundColor $colour
    } else {
        Write-Host $_.Line
    }
}

# ---- Counts ----
$warnings = @($lines | Where-Object { $_.Line -match '\|WARNING\|' }).Count
$errors   = @($lines | Where-Object { $_.Line -match '\|ERROR\|' }).Count
$fetchFailures = @($lines | Where-Object { $_.Line -like '*Image fetch failed*' }).Count
Write-Host ""
Write-Host "Summary" -ForegroundColor Cyan
Write-Host ("  Warnings: {0}   Errors: {1}   Image fetch failures: {2}" -f $warnings, $errors, $fetchFailures)

# ---- Exceptions with stack traces ----
if ($Exceptions) {
    Write-Host ""
    Write-Host "Exceptions involving the plugin" -ForegroundColor Cyan
    $entryStart = '^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}'   # every NINA log entry starts with its time
    $found = 0
    foreach ($f in $files) {
        $all = Get-Content -LiteralPath $f.FullName
        for ($i = 0; $i -lt $all.Count; $i++) {
            if ($all[$i] -notmatch 'Exception') { continue }
            if ($all[$i] -notmatch $entryStart) { continue }      # only the first line of an entry
            # Collect the entry: this line and the following lines up to the next entry.
            $block = @($all[$i])
            $j = $i + 1
            while ($j -lt $all.Count -and $all[$j] -notmatch $entryStart) { $block += $all[$j]; $j++ }
            if (($block -join "`n") -notmatch 'AllskySafetyMonitor') { continue }
            $found++
            Write-Host ""
            Write-Host "--- $($f.Name), line $($i + 1) ---" -ForegroundColor Yellow
            $block | Select-Object -First 60 | ForEach-Object { Write-Host $_ -ForegroundColor Red }
            if ($block.Count -gt 60) { Write-Host "  ... $($block.Count - 60) more lines" }
            $i = $j - 1
        }
    }
    if ($found -eq 0) { Write-Host "  None found." -ForegroundColor Green }
}

# ---- Secrets (REQ-54) ----
if (-not $Secret -and -not $NoSecrets) {
    $entered = Read-Host "Secrets to check, comma separated (Enter to skip)"
    if ($entered) { $Secret = $entered -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ } }
}

if ($Secret) {
    $scan = @($files)
    if (Test-Path $DecisionFolder) {
        $scan += @(Get-ChildItem -Path $DecisionFolder -Filter 'decisions-*.csv' | Where-Object { $_.LastWriteTime -ge $Since })
    }
    Write-Host ""
    Write-Host "Secrets check in $($scan.Count) files (NINA logs and decision logs)" -ForegroundColor Cyan
    $allClear = $true
    $index = 0
    foreach ($s in $Secret) {
        $index++
        $hits = @($scan | Select-String -Pattern $s -SimpleMatch)
        $label = "secret $index ($($s.Substring(0, [Math]::Min(3, $s.Length)))...)"
        if ($hits.Count -eq 0) {
            Write-Host "  PASS  $label not found" -ForegroundColor Green
        } else {
            $allClear = $false
            $where = ($hits | Group-Object Filename | ForEach-Object { "$($_.Name) x$($_.Count)" }) -join ', '
            Write-Host "  FAIL  $label found in: $where" -ForegroundColor Red
        }
    }
    if ($allClear) { Write-Host "  No secrets in the logs." -ForegroundColor Green }
}
