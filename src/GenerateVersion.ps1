param(
    [string]$Version,
    [string]$OutFile
)
$content = "namespace Amethyst { internal static class VersionInfo { public const string PluginVersion = `"$Version`"; } }"
Set-Content -LiteralPath $OutFile -Value $content -Encoding UTF8 -NoNewline