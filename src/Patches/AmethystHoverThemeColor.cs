using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

// 界面优化：把按钮**悬停时那圈绿色高亮/描边**渲染成主题色。
//
// ⚠ 第 164 轮（正解，照搬 FinalSuspect）：
//   参考 `FinalSuspect\Patches\System\ColorPatch.cs`：
//       [HarmonyPatch(typeof(ButtonRolloverHandler))]
//       DoMouseOver_Prefix    → OverColor 是“接受绿”就换成主题色
//       ChangeOutColor_Prefix → 传进来的描边色是绿的就换成主题色
//
//   原版的绿色悬停高亮**不是** `activeSprites` 的颜色，而是
//   `ButtonRolloverHandler.OverColor`（悬停时）/ `ChangeOutColor` 的入参（移出时）。
//   所以只要把这两个地方的**绿色**替换成主题色即可 ——
//   按钮填充、常态图、文字一律不动，也不会“改不回来”（原版自己负责进出场）。
//
// ⚠ 前三版为什么全错：
//   ① 改 `ButtonRolloverHandler.Target/Target2` 的 renderer.color → 跨按钮共享，整图叠紫；
//   ② 把 `activeSprites` + `inactiveSprites` + 文字全部染主题色 → 整个按钮变色且不还原；
//   ③ 只染 `activeSprites` 并手动还原 → activeSprites 是**整张按钮图**，仍然“整个按钮变色”。
//   关键点：**只替换绿色本身**（OverColor / ChangeOutColor 入参），而不是替换某个渲染器的颜色。
[HarmonyPatch(typeof(ButtonRolloverHandler))]
internal static class AmethystHoverThemeColorPatch
{
    private static bool Enabled =>
        AmethystConfig.HoverThemeColor != null && AmethystConfig.HoverThemeColor.Value;

    private static Color Theme
    {
        get { try { return AmethystStyle.Current.Accent; } catch { return Color.white; } }
    }

    // 原版“接受绿”。两种形态都要认：
    //   · OverColor  —— 纯绿 (0,1,0) 或 Palette.AcceptedGreen
    //   · 描边色     —— (0,1,0.164,1) 那种带一点蓝的绿
    //
    // ⚠ v1.1.6：本轮试过把它放宽成"任何有彩色都算高光色"（想消掉主界面按钮
    //   那圈浅蓝描边），结果反而干扰了其它界面的按钮配色，已回退为**只认绿色**。
    private static bool IsGreenHighlight(Color c)
    {
        try { if (c == Palette.AcceptedGreen) return true; } catch { }
        if (c.r > 0.01f) return false;
        if (c.a < 0.99f) return false;
        return c.g > 0.99f && c.b >= -0.01f && c.b <= 0.18f;
    }

    // 悬停：原版会把按钮的 OverColor 贴上去，绿的就换主题色。
    [HarmonyPatch("DoMouseOver")]
    [HarmonyPrefix]
    private static void DoMouseOver_Prefix(ButtonRolloverHandler __instance)
    {
        if (!Enabled || __instance == null) return;
        try
        {
            if (IsGreenHighlight(__instance.OverColor))
                __instance.OverColor = Theme;
        }
        catch { }
    }

    // 移出：原版会把描边色传进 ChangeOutColor，同样只替换绿色。
    [HarmonyPatch("ChangeOutColor")]
    [HarmonyPrefix]
    private static void ChangeOutColor_Prefix(ButtonRolloverHandler __instance, ref Color color)
    {
        if (!Enabled || __instance == null) return;
        try
        {
            if (IsGreenHighlight(color)) color = Theme;
        }
        catch { }
    }
}
