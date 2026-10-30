using HarmonyLib;

namespace Amethyst.Patches;

// ===== 启动闸门（第 214 轮：启动动画已按要求**移除**）=====
//
// 历史沿革（保留教训，避免以后重蹈覆辙）：
//   · 第 127/128 轮：拦 `AllowFinishLoadingScene()` 会导致永久卡加载（它只被调用一次）。
//   · 第 137/139 轮：改用按住 `SplashManager.Update` + 放行后持续 Nudge ⇒ 可用但开销大。
//   · 第 200 轮：启动界面移除后闸门停用（否则永久卡住）。
//   · 第 211~213 轮：尝试用 IMGUI / 世界空间 SpriteRenderer 重做启动动画，实测均无法正常
//     工作（IMGUI 在启动场景只出背景；参考移植版也未达预期）。**按用户要求整体移除。**
//
// 现在：不再拦截 `SplashManager.Update`，也**不再拦截** `AllowFinishLoadingScene()`，
//       游戏完全按原版流程从启动画面进入主界面。
//       仅保留 `Release()` 空实现，供 `AmethystVersionChecker.SafetyNet` 的兜底调用不变。
internal static class AmethystStartupGatePatch
{
    // 兼容保留：更新检查不再拦启动，这里什么都不用做。
    internal static void Release()
    {
    }
}
