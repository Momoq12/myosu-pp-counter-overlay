$ErrorActionPreference = 'Stop'
$runtimeVersion = 'v24.21.0'
$archiveName = "node-$runtimeVersion-win-x64.zip"
$downloadRoot = "https://nodejs.org/dist/$runtimeVersion"
$cache = Join-Path $PSScriptRoot '.cache'
New-Item -ItemType Directory -Force $cache | Out-Null
$archivePath = Join-Path $cache $archiveName
Invoke-WebRequest "$downloadRoot/$archiveName" -OutFile $archivePath
$sums = (Invoke-WebRequest "$downloadRoot/SHASUMS256.txt").Content
$line = ($sums -split "`n" | Where-Object { $_.Trim().EndsWith("  $archiveName") } | Select-Object -First 1)
if (-not $line) { throw 'Runtime archive checksum was not found' }
$expected = ($line.Trim() -split '\s+')[0]
if ((Get-FileHash $archivePath -Algorithm SHA256).Hash -ne $expected) { throw 'Runtime checksum did not match' }
Expand-Archive -LiteralPath $archivePath -DestinationPath $cache -Force
Copy-Item (Join-Path $cache "node-$runtimeVersion-win-x64\node.exe") (Join-Path $PSScriptRoot 'calculator\node.exe') -Force
Write-Output 'Pinned offline runtime restored. Run build.ps1 next.'
