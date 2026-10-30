<#
    Deploy.ps1 -- copies the build output into the game's BepInEx\plugins\ folder.
    Invoked automatically by the DeployToGame target in Amethyst.csproj after a
    Release build.

    NOTE: this file is intentionally ASCII-only. It is run via `powershell`
    (Windows PowerShell 5.1), which decodes .ps1 files as ANSI unless they carry a
    UTF-8 BOM. Non-ASCII comments previously got mangled into unparseable text and
    broke the whole script, so all messages here stay in plain ASCII.

    Design rules:
      1. Only ever write to the plugins folder (the place BepInEx actually loads).
      2. Never touch config (that holds the user's settings).
      3. If the target is locked (game running), warn and skip -- do NOT fail the build.
      4. Verify the copy by size + SHA256, so a silent partial write cannot slip through.
      5. Keep the previous file as .bak for easy rollback.

    Always exits 0: deployment is a convenience and must never turn a successful
    compile into a failed one.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$TargetDir,
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'

function Write-Info($msg) { Write-Host "[deploy] $msg" }
function Write-Warn($msg) { Write-Host "[deploy][warn] $msg" -ForegroundColor Yellow }

# Uses .NET instead of Get-FileHash: under MSBuild's Exec the Utility module is not
# always autoloaded, and the cmdlet would then throw and abort the verification.
function Get-Sha256($path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $fs = [System.IO.File]::Open($path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        try { return [BitConverter]::ToString($sha.ComputeHash($fs)).Replace('-', '') }
        finally { $fs.Dispose() }
    }
    finally { $sha.Dispose() }
}

try {
    if (-not (Test-Path -LiteralPath $Source -PathType Leaf)) {
        Write-Warn "source not found: $Source (skipping deploy)"
        exit 0
    }

    if (-not (Test-Path -LiteralPath $TargetDir -PathType Container)) {
        Write-Warn "target dir not found: $TargetDir (skipping deploy)"
        exit 0
    }

    $name = [System.IO.Path]::GetFileName($Source)
    $dest = Join-Path $TargetDir $name

    # ---- Is the game running? It would hold an exclusive lock on the DLL. ----
    $proc = Get-Process -Name 'Among Us' -ErrorAction SilentlyContinue
    if ($proc) {
        Write-Warn "Among Us is running (PID: $($proc.Id -join ','))."
        Write-Warn "Skipped deploy: the game holds the DLL. Close the game and rebuild to deploy."
        exit 0
    }

    # ---- Back up the existing file ----
    if (Test-Path -LiteralPath $dest -PathType Leaf) {
        try {
            Copy-Item -LiteralPath $dest -Destination "$dest.bak" -Force
        } catch {
            Write-Warn "could not back up old file (continuing): $($_.Exception.Message)"
        }
    }

    # ---- Copy ----
    Copy-Item -LiteralPath $Source -Destination $dest -Force

    # ---- Verify: size AND SHA256 must match ----
    $srcLen  = (Get-Item -LiteralPath $Source).Length
    $dstLen  = (Get-Item -LiteralPath $dest).Length
    $srcHash = Get-Sha256 $Source
    $dstHash = Get-Sha256 $dest

    if ($srcLen -ne $dstLen -or $srcHash -ne $dstHash) {
        Write-Warn "VERIFY FAILED! source $srcLen B / target $dstLen B -- the file may be incomplete."
        Write-Warn "Please check manually: $dest"
        exit 0
    }

    $ver = if ($Version) { " v$Version" } else { '' }
    Write-Info "deployed$ver -> $dest"
    Write-Info "  size $dstLen B  SHA256 $($dstHash.Substring(0,16))..."
    Write-Info "  restart Among Us to apply."
}
catch {
    # A deploy failure must never affect the build result.
    Write-Warn "deploy error (ignored, build result unaffected): $($_.Exception.Message)"
}

exit 0
