using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 模组水印（右上角那个模组图标）。
//
// ===== 历史（保留，便于以后不再踩同一个坑）=====
//
// 用户先后反馈过三次问题：
//   ① "右上角的模组图标一直位移" —— 旧实现挂在 `AmethystHud.Update` 上**每帧**写
//      `localScale`，把游戏自己的入场动画一帧一帧按回去；同时又"每帧检查 enabled、
//      一旦发现 disabled 就重播 ShowModStamp()"，动画反复重播 ⇒ 位置来回跳。
//   ② "模组图标忽大忽小" —— 本文件与 `AmethystMainMenuStyle` 的 `ShrinkModStamp()`
//      以不同频率写同一个 Transform，互相覆盖。（后者已整体删除。）
//   ③ "右上角模组图标不显示" —— 旧实现用错了"可见性"判据，见下。
//
// ===== 参考实现（三份，做法一致）=====
//   · BetterAmongUs-1.3.4 `Patches\Client\Managers\ModManagerPatch.cs`
//       `[HarmonyPatch(typeof(ModManager), "LateUpdate")][HarmonyPostfix]`
//       → 加载完成后**每帧** `__instance.ShowModStamp()`；`active` 时替换贴图。
//   · FinalSuspect-1.4 `Patches\System\CredentialsPatch.cs`
//       `ModManager.LateUpdate` 的 Prefix **每帧** `ShowModStamp()`，Postfix 每帧重定位；
//       主菜单一次性 `ModStamp.localScale = 0.3` + 换贴图。
//   · OnyxMenu-1.2.2 `Patches\ModStampPatches.cs`
//       自带驱动 MonoBehaviour 的 LateUpdate：**以 `((Renderer)stamp).enabled` 判断**
//       图标是否已显示，未显示才再调 `ShowModStamp()`。
//
// ===== "图标不显示"的根因（有参考代码 + 历史副本双重为证）=====
//
//   **旧实现用错了"可见性"判据。**
//
//   Hydra-2.0.0 隐藏图标用的是 `ModManager.Instance.ModStamp.enabled = false;`
//   （Hydra.cs:71），OnyxMenu 判断图标是否已显示用的是 `((Renderer)stamp).enabled`。
//   即：**图标的开关是 `Renderer.enabled`，不是 `gameObject.activeInHierarchy`。**
//
//   旧实现写的是：
//       if (_stamped && _stamp != null && _stamp.gameObject.activeInHierarchy) return;
//   游戏把 `enabled` 置 false 时，`activeInHierarchy` **仍然是 true** ⇒ 直接 return，
//   既不再调 `ShowModStamp()`，也永远不再点亮 ⇒ **图标不显示**。
//   项目历史副本 `AMECE - 副本\src\Patches\ModStampPatch.cs` 里能用的一版，
//   写的正是 `if (!stamp.enabled) { _mgr.ShowModStamp(); }` —— 可对照。
//
// ===== 现在的做法 =====
//   1. 可见性判据用 `Renderer.enabled`（与 OnyxMenu / Hydra 一致）。
//   2. 主驱动挂 `ModManager.LateUpdate` 的 Postfix、**每帧**调用
//      （与 BetterAmongUs / FinalSuspect 一致，它们都靠每帧调用来维持图标常显）；
//      `AmethystHud` 保留低频兜底，但**不**再套 `Slot.ModStamp` 的二次帧槽判断
//      （本块本身已是 0.1s 节流，再叠一个每帧轮转的 32 槽调度会形成两个不同周期，
//        可能长期不命中）。
//   3. 贴图**每帧**做一次指针比较（零分配），不一致才换。
//   4. 缩放/颜色**每个 stamp 实例只写一次**（避免与入场动画打架）。
//
// ===== 日志 =====
//   功能已实机确认正常（日志里出现过 `[ModStamp] 模组图标已就位`），
//   按要求**已全部移除**该功能的诊断日志，避免每次启动刷屏。
//   若日后再次出现"图标不显示"，在这里临时恢复一行 LogInfo 即可。
internal static class ModStampState
{
    // 目标缩放：沿用历史可用的 0.25（第 189 轮文档结论：0.25 更小、更适合当角标）。
    // ⚠ 不要再改成 0.3：那会让图标变大，且历史上"忽大忽小"就是两个模块写不同缩放值造成的。
    private static readonly Vector3 TargetScale = new Vector3(0.25f, 0.25f, 0.25f);

    private const float ShowRetryInterval = 0.5f;

    private static ModManager _mgr;
    private static SpriteRenderer _stamp;
    private static SpriteRenderer _styled;   // 已写过缩放/颜色的 stamp 实例
    private static float _nextShow;

    /// <summary>由 `ModManager.LateUpdate` 的 Postfix 每帧调用；`AmethystHud` 亦作为兜底调用。</summary>
    internal static void Tick()
    {
        try
        {
            // ModManager 是 DestroyableSingleton；`Instance` 缺失时 getter 可能做一次全场景查找，
            // 所以只在自己的引用失效时才去读它。
            if (_mgr == null)
            {
                try { _mgr = ModManager.Instance; }
                catch { _mgr = null; }
            }
            if (_mgr != null && _mgr.localCamera == null)
            {
                Camera cam = Camera.main;
                if (cam == null) cam = UnityEngine.Object.FindObjectOfType<Camera>();
                if (cam != null) _mgr.localCamera = cam;
            }

            SpriteRenderer stamp = Resolve();
            if (stamp == null) return;

            // ① 可见性：以 Renderer.enabled 为准（见文件头"图标不显示的根因"）。
            bool visible = IsEnabled(stamp);
            if (!visible && Time.unscaledTime >= _nextShow)
            {
                _nextShow = Time.unscaledTime + ShowRetryInterval;
                if (_mgr != null) { try { _mgr.ShowModStamp(); } catch { } }
                visible = IsEnabled(stamp);   // 调完立刻复查
            }
            if (!visible) return;

            // ② 贴图：每帧比一次指针（零分配），不一致才换。
            Sprite sprite = AmethystResources.ModStamp;
            if (sprite != null && stamp.sprite != sprite) stamp.sprite = sprite;

            _stamp = stamp;

            // ③ 缩放/颜色：每个 stamp 实例只写一次，避免与游戏自己的入场动画互相覆盖
            //    （这正是历史上"位移 / 忽大忽小"的成因）。
            if (_styled == stamp) return;

            stamp.color = new Color(1f, 1f, 1f, 0.4f);
            stamp.transform.localScale = TargetScale;
            _styled = stamp;
        }
        catch { }
    }

    private static bool IsEnabled(SpriteRenderer sr)
    {
        try { return sr != null && ((Renderer)sr).enabled; }
        catch { return false; }
    }

    // 主驱动：与 BetterAmongUs / FinalSuspect 一致，挂 ModManager.LateUpdate 的 Postfix，
    // 每帧调用一次。它们正是靠"每帧调用 ShowModStamp()"来维持图标常显的。
    [HarmonyPatch(typeof(ModManager), "LateUpdate")]
    [HarmonyPostfix]
    private static void LateUpdate_Postfix(ModManager __instance)
    {
        try
        {
            if (_mgr == null) _mgr = __instance;
            Tick();
        }
        catch { }
    }

    private static SpriteRenderer Resolve()
    {
        try
        {
            if (_stamp != null) return _stamp;
            if (_mgr != null && _mgr.ModStamp != null) return _mgr.ModStamp;
        }
        catch { }

        try
        {
            GameObject go = GameObject.Find("ModStamp");
            if (go != null)
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) return sr;
            }
        }
        catch { }

        return null;
    }

    // 场景切换后 ModStamp 可能被重建，重置状态以便下次重新贴一次
    internal static void Reset()
    {
        _mgr = null;
        _stamp = null;
        _styled = null;
        _nextShow = 0f;
    }
}
