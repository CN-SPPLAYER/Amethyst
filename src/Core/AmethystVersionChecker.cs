using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Amethyst;

public sealed class AmethystVersionChecker : MonoBehaviour
{
    public AmethystVersionChecker(IntPtr ptr) : base(ptr) { }

    internal enum CheckState { Checking, Ok, Failed }

    private const string Url = AmethystRelease.ApiUrl;
    private const int TimeoutSeconds = 8;

    internal static bool UpdateAvailable;
    internal static string LatestVersion = string.Empty;

    internal static CheckState State = CheckState.Checking;
    internal static bool Outdated;    
    internal static bool UpToDate;    
    internal static bool Finished => State != CheckState.Checking;

    internal static bool GatePending = true;
    internal static void ReleaseGate() { GatePending = false; }

    internal static void ForceFailed()
    {
        if (State == CheckState.Checking) State = CheckState.Failed;
    }

    private void Start()
    {
        try { StartCoroutine(Fetch().WrapToIl2Cpp()); }
        catch { State = CheckState.Failed; }

        try { StartCoroutine(SafetyNet().WrapToIl2Cpp()); } catch { }
    }

    private static IEnumerator SafetyNet()
    {
        float t = 0f;
        while (t < 13f)
        {
            if (!GatePending) yield break;   
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        ForceFailed();
        try { Patches.AmethystStartupGatePatch.Release(); }
        catch { GatePending = false; }
    }

    private static IEnumerator Fetch()
    {
        UnityWebRequest req = null;
        try
        {
            req = UnityWebRequest.Get(Url);
            if (req != null)
            {
                req.timeout = TimeoutSeconds;
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.SetRequestHeader("User-Agent", "Amethyst-Updater");
            }
        }
        catch { }

        if (req == null)
        {
            State = CheckState.Failed;
            yield break;
        }

        yield return req.SendWebRequest();

        bool ok = false;
        try
        {
            if (req.result == UnityWebRequest.Result.Success && req.downloadHandler != null)
            {
                string text = req.downloadHandler.text;

                if (AmethystRelease.TryParse(text, out AmethystRelease.Info rel)
                    && !string.IsNullOrEmpty(rel.Version))
                {
                    LatestVersion = rel.Version;

                    int cmp = AmethystRelease.Compare(AmethystPlugin.PluginVersion, rel.Version);
                    Outdated = cmp > 0;
                    UpToDate = cmp <= 0;   
                    UpdateAvailable = Outdated;
                    ok = true;
                }
            }
        }
        catch { }
        finally
        {
            try { req.Dispose(); } catch { }
        }

        State = ok ? CheckState.Ok : CheckState.Failed;
    }
}
