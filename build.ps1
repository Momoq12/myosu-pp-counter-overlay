$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
Push-Location $PSScriptRoot
try {
    & $compiler /nologo /target:winexe /platform:x64 /optimize+ /out:Myosu.exe /win32icon:App.ico /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll src\Memory.cs src\Program.cs src\Overlay.cs src\Checks.cs src\Performance.cs src\AssemblyInfo.cs
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    Write-Output 'Built Myosu.exe'
} finally { Pop-Location }


