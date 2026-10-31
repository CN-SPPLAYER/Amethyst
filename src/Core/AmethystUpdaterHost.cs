using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystUpdaterHost : MonoBehaviour
{
    public AmethystUpdaterHost(IntPtr ptr) : base(ptr) { }

    internal static AmethystUpdaterHost Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(Instance, this)) Instance = null;
    }

    internal void BeginCheck()
    {
        try
        {
            StopAllCoroutines();
            StartCoroutine(AmethystUpdater.CoCheck().WrapToIl2Cpp());
        }
        catch (Exception ex)
        {
            AmethystUpdater.Fail(AmethystText.T("UpdateCheckFailed"));
            try { AmethystPlugin.Logger?.LogWarning("[Updater] start check failed: " + ex.Message); } catch { }
        }
    }

    internal void BeginDownload()
    {
        try
        {
            StopAllCoroutines();
            StartCoroutine(AmethystUpdater.CoDownload().WrapToIl2Cpp());
        }
        catch (Exception ex)
        {
            AmethystUpdater.Fail(AmethystText.T("UpdateDownloadFailed"));
            try { AmethystPlugin.Logger?.LogWarning("[Updater] start download failed: " + ex.Message); } catch { }
        }
    }
}
