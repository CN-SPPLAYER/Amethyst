using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;

namespace Amethyst;

// 更新流程的协程宿主。
//
// 为什么不放在 `AmethystUpdater`（静态类）里：IL2CPP 下 `StartCoroutine`
// 必须由 MonoBehaviour 实例发起，静态类没有 `StartCoroutine`。
//
// 为什么用单例：弹窗按钮点击时（`AmethystUpdateDialog`）需要拉起下载，
// 但点击回调里拿不到宿主引用；单例是这里最小可行的连接方式。
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

    // 由主界面「检查更新」按钮调用
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

    // 由弹窗「立即下载」按钮调用
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
