using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Amethyst;

internal static class AmethystDependencies
{
    
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

                    if (!File.Exists(dest) || new FileInfo(dest).Length != s.Length)
                    {
                        using var fs = File.Create(dest);
                        s.CopyTo(fs);
                    }

                    if (IsAssembly(lib.File))
                    {
                        Paths[lib.File] = dest;
                        
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
