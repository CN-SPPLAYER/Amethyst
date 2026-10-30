using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Amethyst;

// 主页「关于」卡片用的本机信息：**操作系统 / 游戏版本 / BepInEx 版本 / 已加载模组数量**。
//
// 全部做了兜底：任何一项取不到就显示 "—"，绝不因为读信息把菜单绘制打断。
// 这些值在游戏运行期间不会变，所以**只算一次并缓存**（模组数量在启动后就固定了）。
internal static class AmethystSysInfo
{
    private static string _os, _game, _bepInEx, _mods;
    private static bool _osDone, _gameDone, _bepInExDone, _modsDone;

    internal static string Os()
    {
        if (!_osDone) { _os = CalcOs(); _osDone = true; }
        return _os;
    }

    internal static string Game()
    {
        if (!_gameDone) { _game = CalcGame(); _gameDone = true; }
        return _game;
    }

    // ⚠ 方法名**不能**叫 BepInEx：那会在本类里把命名空间 `BepInEx` 遮住（CS0119）。
    internal static string BepInExVersion()
    {
        if (!_bepInExDone) { _bepInEx = CalcBepInEx(); _bepInExDone = true; }
        return _bepInEx;
    }

    internal static string ModCount()
    {
        if (!_modsDone) { _mods = CalcModCount(); _modsDone = true; }
        return _mods;
    }

    // ===== 操作系统 =====
    // 例：Windows 10.0.22631 (X64)
    private static string CalcOs()
    {
        try
        {
            string desc = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
            if (!string.IsNullOrEmpty(desc))
            {
                if (desc.StartsWith("Microsoft ", StringComparison.Ordinal)) desc = desc.Substring("Microsoft ".Length);
                string arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
                return desc + " (" + arch + ")";
            }
        }
        catch { }

        try { return Environment.OSVersion.VersionString; } catch { }
        return "—";
    }

    // ===== 游戏版本（Unity Application.version）=====
    private static string CalcGame()
    {
        try
        {
            string v = Application.version;
            if (!string.IsNullOrEmpty(v)) return v;
        }
        catch { }
        return "—";
    }

    // ===== BepInEx 版本 =====
    // ⚠ `Paths.DisplayBepInExVersion` / `BepInExVersion` 的类型是 `SemanticVersioning.Version`，
    //   而本工程**没有引用 SemanticVersioning.dll**（它只存在于游戏 core 目录），
    //   直接写属性会 CS0012；所以这里用反射取，再 ToString()。
    private static string CalcBepInEx()
    {
        string v = PathsProp("DisplayBepInExVersion");
        if (string.IsNullOrEmpty(v)) v = PathsProp("BepInExVersion");
        if (!string.IsNullOrEmpty(v)) return v;

        try { return typeof(BepInEx.Paths).Assembly.GetName().Version.ToString(); } catch { }
        return "—";
    }

    private static string PathsProp(string name)
    {
        try
        {
            PropertyInfo p = typeof(BepInEx.Paths).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            object v = p != null ? p.GetValue(null, null) : null;
            string s = v != null ? v.ToString() : null;
            return string.IsNullOrEmpty(s) ? null : s;
        }
        catch { return null; }
    }

    // ===== 已加载模组（插件）数量 =====
    private static string CalcModCount()
    {
        // 首选：BepInEx 链式加载器里真正加载成功的插件表
        try
        {
            var chain = BepInEx.Unity.IL2CPP.IL2CPPChainloader.Instance;
            if (chain != null && chain.Plugins != null) return chain.Plugins.Count.ToString();
        }
        catch { }

        // 兜底：数 plugins 目录下的 dll（含子目录）
        try
        {
            string dir = BepInEx.Paths.PluginPath;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories).Length.ToString();
        }
        catch { }

        return "—";
    }
}
