using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Amethyst;

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

    private static string CalcModCount()
    {
        
        try
        {
            var chain = BepInEx.Unity.IL2CPP.IL2CPPChainloader.Instance;
            if (chain != null && chain.Plugins != null) return chain.Plugins.Count.ToString();
        }
        catch { }

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
