# Build the Android / Starlight variant of Amethyst (AnyCPU, .NET 6).
#
#   - Game assemblies come from AmongUs.GameLibs.Android (hollowed) and are
#     converted to interop references by BepInEx.IL2CPP.MSBuild.
#   - Output: Amethyst_<version>_Android.dll in the repo-root dist\ folder,
#     plus a copy in the build output folder.
#
# NuGet sources: nuget.org + https://nuget.bepinex.dev/v3/index.json
# (the first restore downloads the Il2CppInterop generator; network required)

$ErrorActionPreference = 'Stop'

$proj = Join-Path $PSScriptRoot 'Amethyst.Android.csproj'

dotnet build $proj -c Release
if ($LASTEXITCODE -ne 0) { throw "Android build failed" }

$outDir = Join-Path $PSScriptRoot 'bin\Release\net6.0'
$out = Join-Path $outDir 'Amethyst.dll'
if (-not (Test-Path -LiteralPath $out)) { throw "missing output: $out" }

$ver = 'unknown'
$m = Select-String -Path $proj -Pattern '<Version>([^<]+)</Version>'
if ($m -and $m.Matches.Count -gt 0) { $ver = $m.Matches[0].Groups[1].Value }

$dist = Join-Path (Split-Path $PSScriptRoot -Parent) 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$name = 'Amethyst_' + $ver + '_Android.dll'
$dst = Join-Path $dist $name
$outCopy = $outDir + '\' + $name

Copy-Item -LiteralPath $out -Destination $dst -Force
Copy-Item -LiteralPath $out -Destination $outCopy -Force

$hash = (Get-FileHash $dst -Algorithm SHA256).Hash
$size = (Get-Item $dst).Length
Write-Host ""
Write-Host "built : $dst"
Write-Host "copy  : $outCopy"
Write-Host "size  : $size B"
Write-Host "sha256: $hash"
