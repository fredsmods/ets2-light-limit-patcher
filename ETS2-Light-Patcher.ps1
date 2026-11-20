# SPDX-License-Identifier: MIT
# ETS2 Light Limit Patcher - PowerShell launcher
# Compiles src\LightPatcher.cs (readable C# source, same folder tree) in memory with the compiler that ships
# with Windows and opens the patcher window. No exe, no download, nothing is installed.
param(
    [string]$ShotPath,     # developer option: render the window to a PNG and exit
    [string]$ExePath       # developer option: pre-select an exe for -ShotPath
)
$ErrorActionPreference = 'Stop'

# WinForms needs a single-threaded apartment; relaunch once with -STA if necessary
if ([Threading.Thread]::CurrentThread.ApartmentState -ne 'STA') {
    $a = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-STA', '-File', "`"$PSCommandPath`"")
    if ($ShotPath) { $a += @('-ShotPath', "`"$ShotPath`"") }
    if ($ExePath)  { $a += @('-ExePath',  "`"$ExePath`"") }
    $p = Start-Process -FilePath (Join-Path $PSHOME 'powershell.exe') -ArgumentList $a -Wait -PassThru
    exit $p.ExitCode
}

$root   = Split-Path -Parent $PSCommandPath
$source = Join-Path $root 'src\LightPatcher.cs'
$assets = Join-Path $root 'assets'
try {
    if (-not (Test-Path $source)) { throw "Missing $source - extract the whole zip, do not move the .ps1 alone." }
    Add-Type -Path $source -ReferencedAssemblies 'System.Windows.Forms', 'System.Drawing'
    if ($ShotPath) { [Launcher]::Shot($assets, $ShotPath, $ExePath) } else { [Launcher]::Run($assets) }
}
catch {
    Add-Type -AssemblyName System.Windows.Forms
    [void][Windows.Forms.MessageBox]::Show("ETS2 Light Limit Patcher could not start:`n`n" + $_.Exception.Message,
        'ETS2 Light Limit Patcher', 'OK', 'Error')
    exit 1
}
