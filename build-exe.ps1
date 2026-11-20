# Rebuilds prebuilt\ETS2-Light-Patcher.exe from src\LightPatcher.cs with the C# compiler that ships with Windows.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
New-Item -ItemType Directory -Force "$root\assets", "$root\prebuilt" | Out-Null
& $csc /nologo /target:winexe /optimize+ /codepage:65001 "/out:$root\prebuilt\ETS2-Light-Patcher.exe" `
    "/resource:$root\assets\bg.jpg,bg.jpg" "/resource:$root\assets\bulb.png,bulb.png" `
    /r:System.Windows.Forms.dll /r:System.Drawing.dll "$root\src\LightPatcher.cs"
if ($LASTEXITCODE -ne 0) { throw 'csc failed' }