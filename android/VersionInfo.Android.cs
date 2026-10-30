namespace Amethyst
{
    // Android 工程不跑 GenerateVersion.ps1，直接写死版本号（与 csproj 的 <Version> 保持一致）。
    internal static class VersionInfo
    {
        public const string PluginVersion = "1.1.7";
    }
}
