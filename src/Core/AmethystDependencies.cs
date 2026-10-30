using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Amethyst;

// 音乐播放器解码依赖（NAudio / NVorbis）的运行时释放与解析。
//
//   · 编译期：csproj 把 NuGet 包里的 DLL 作为 EmbeddedResource 打进本 DLL；
//   · 运行时：首次用到时释放到「Amethyst 数据目录\libs」，并挂 AppDomain.AssemblyResolve
//             用 Assembly.LoadFile 加载。
//
// 为什么必须这样：BepInEx/IL2CPP 不会自动解析模组目录旁的 NuGet 依赖。
// 只写 PackageReference 的话编译能过，运行时抛 FileNotFoundException: NAudio。
//
// NAudio / NVorbis 均为 MIT 许可的第三方开源库，许可证全文见释放目录下的
// THIRD-PARTY-NOTICES.txt（随依赖一起释放）。
internal static class AmethystDependencies
{
    // 逻辑名 -> 文件名（与 csproj 的 LogicalName 一一对应）
    //
    // ⚠ 后三项是许可证文件，**不是**可加载的程序集：MIT 要求随分发保留版权与许可声明，
    //   所以它们只被释放到 libs 目录，不参与 AssemblyResolve。
    private static readonly (string Res, string File)[] Libs =
    {
        ("Amethyst.libs.NAudio.dll", "NAudio.dll"),
        ("Amethyst.libs.NAudio.Core.dll", "NAudio.Core.dll"),
        ("Amethyst.libs.NAudio.WinMM.dll", "NAudio.WinMM.dll"),
        ("Amethyst.libs.NVorbis.dll", "NVorbis.dll"),
        ("Amethyst.libs.THIRD-PARTY-NOTICES.txt", "THIRD-PARTY-NOTICES.txt"),
        ("Amethyst.libs.NAudio-LICENSE.txt", "NAudio-LICENSE.txt"),
        ("Amethyst.libs.NVorbis-LICENSE.txt", "NVorbis-LICENSE.txt"),
    };

    // 只有程序集才注册进 AssemblyResolve
    private static bool IsAssembly(string file) => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> Paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static bool _ready;

    internal static void Setup()
    {
        try
        {
            if (_ready) return;
            _ready = true;

            string dir = Path.Combine(AmethystPlatform.DataDir, "libs");
            try { Directory.CreateDirectory(dir); } catch { }

            Assembly self = typeof(AmethystDependencies).Assembly;
            foreach (var lib in Libs)
            {
                try
                {
                    string dest = Path.Combine(dir, lib.File);
                    using Stream s = self.GetManifestResourceStream(lib.Res);
                    if (s == null)
                    {
                        AmethystPlugin.Logger?.LogWarning($"[Deps] embedded resource missing: {lib.Res}");
                        continue;
                    }

                    // 已存在且大小一致就跳过（避免每次启动重复写盘）
                    if (!File.Exists(dest) || new FileInfo(dest).Length != s.Length)
                    {
                        using var fs = File.Create(dest);
                        s.CopyTo(fs);
                    }

                    // 只有程序集才登记给 AssemblyResolve；许可证文本只是释放出来
                    if (IsAssembly(lib.File))
                    {
                        Paths[lib.File] = dest;
                        // 去掉扩展名作为「程序集短名」的键，AssemblyResolve 用的是短名
                        Paths[Path.GetFileNameWithoutExtension(lib.File)] = dest;
                    }
                }
                catch (Exception ex)
                {
                    AmethystPlugin.Logger?.LogWarning($"[Deps] extract {lib.File} failed: {ex.Message}");
                }
            }

            AppDomain.CurrentDomain.AssemblyResolve += OnResolve;
        }
        catch (Exception ex)
        {
            AmethystPlugin.Logger?.LogWarning($"[Deps] setup failed: {ex.Message}");
        }
    }

    private static Assembly OnResolve(object sender, ResolveEventArgs args)
    {
        try
        {
            string name = new AssemblyName(args.Name).Name;
            if (name == null) return null;
            return Paths.TryGetValue(name, out string p) && File.Exists(p) ? Assembly.LoadFile(p) : null;
        }
        catch
        {
            return null;
        }
    }

    internal static void FlushLog()
    {
    }
}
