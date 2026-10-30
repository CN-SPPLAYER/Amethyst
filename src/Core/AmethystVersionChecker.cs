using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace Amethyst;

// 版本检查：启动时读取 GitHub Releases 最新发布，与自身版本比较。
//
// ⚠ 第 267 轮：**不再读取 `Version.txt`**。
//   `Version.txt` 是仓库里的普通文件，发 release 时不会自动同步，
//   忘记改就会一直提示旧版本。现在直接读 Releases API（`/releases/latest`），
//   与「按 tag 格式发布 release」这一实际流程一致，不需要额外维护文件。
//
// 第一百二十六轮：加入**检查状态机**，供启动界面做
// 「检查更新 → 展示结果（红/绿，闪两下）→ 才放行进入主界面」。
public sealed class AmethystVersionChecker : MonoBehaviour
{
    public AmethystVersionChecker(IntPtr ptr) : base(ptr) { }

    internal enum CheckState { Checking, Ok, Failed }

    private const string Url = AmethystRelease.ApiUrl;
    private const int TimeoutSeconds = 8;

    // 旧字段：主菜单的"有新版本"提示仍在用
    internal static bool UpdateAvailable;
    internal static string LatestVersion = string.Empty;

    // 状态
    internal static CheckState State = CheckState.Checking;
    internal static bool Outdated;    // 当前版本 < 远程
    internal static bool UpToDate;    // 版本相同
    internal static bool Finished => State != CheckState.Checking;

    // ===== 启动闸门 =====
    // 检查期间挡住"启动画面 → 主界面"的场景切换（见 Patches\AmethystStartupGate.cs），
    // 由启动界面在结果展示完毕后放行。
    internal static bool GatePending = true;
    internal static void ReleaseGate() { GatePending = false; }

    // 硬超时兜底：无论网络怎么卡，也不能把游戏卡在启动画面
    internal static void ForceFailed()
    {
        if (State == CheckState.Checking) State = CheckState.Failed;
    }

    private void Start()
    {
        try { StartCoroutine(Fetch().WrapToIl2Cpp()); }
        catch { State = CheckState.Failed; }

        // 独立安全网：不依赖启动界面，13 秒后无论如何都放行启动
        try { StartCoroutine(SafetyNet().WrapToIl2Cpp()); } catch { }
    }

    private static IEnumerator SafetyNet()
    {
        float t = 0f;
        while (t < 13f)
        {
            if (!GatePending) yield break;   // 已被正常放行
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
                    UpToDate = cmp <= 0;   // 无法判定时按「已是最新」，避免误报过期
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
