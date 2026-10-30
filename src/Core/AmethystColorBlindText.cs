using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst;

// 颜色显示优化（色盲文字）：在每位玩家附近显示其颜色名称（按该颜色着色）。
//
// ===== 第 179 轮性能重写 =====
//
// 旧实现每个 LateUpdate 都全量遍历玩家、每名玩家每帧读：
//     pc.cosmetics / pc.cosmetics.colorBlindText / pc.cosmetics.bodyMatProperties.ColorId
//     text.text（★ 读 TMP.text 会分配托管字符串）
// 并且每帧调用 `Palette.GetColorName(colorId)`（原生调用 + 字符串）。
//
// 现在颜色 id 的读取、颜色名的构建、\"是否与当前文本相同\"的判断，
// 全部搬到 `AmethystPlayerCacheStore` 的错峰刷新里（每人每 20 帧一次）；
// 本组件的每帧循环只把缓存算好的待写值落到 TMP 上，没有跨边界读取与分配。
public sealed class AmethystColorBlindText : MonoBehaviour
{
    public AmethystColorBlindText(IntPtr ptr) : base(ptr) { }

    internal static bool On => AmethystConfig.ColorBlindText != null && AmethystConfig.ColorBlindText.Value;

    private void LateUpdate()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_NameFlush);
    try { LateUpdateInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_NameFlush); }
    }

    private void LateUpdateInner()
    {
            if (!On) return;

            try
            {
                var list = AmethystPlayerCacheStore.All;
                for (int i = 0; i < list.Count; i++)
                {
                    AmethystPlayerCache c = list[i];
                    if (c == null) continue;

                    // 第 182 轮：与 `AmethystNameRendering.FlushNames` 共用同一份就地刷新，
                    // 保证名字与色盲文字在**同一帧**一起生效（谁先跑到谁负责刷）。
                    AmethystPlayerCacheStore.RefreshIfDirty(c);

                    if (c.PendingColorBlindId == int.MinValue) continue;

                    TextMeshPro text = c.ColorBlindText;
                    c.PendingColorBlindId = int.MinValue;   // 无论成败都清掉，避免每帧重试
                    if (text == null) continue;

                    int id = c.ShownColorId >= 0 ? c.ShownColorId : c.ColorId;
                    string want = AmethystColors.ColoredColorName(id);
                    if (string.IsNullOrEmpty(want)) continue;

                    try
                    {
                        text.richText = true;
                        if (text.text != want) text.text = want;
                        if (!text.enabled) text.enabled = true;
                        // 第 184 轮：记下写入目标实例（理由同名字那条）
                        c.WrittenColorBlindId = id;
                        c.WrittenForColorBlind = text;
                    }
                    catch { }
                }
            }
            catch { }
    
    }
}

// 直接接管游戏自身的色盲文字取值：无论自由模式、大厅还是对局，
// 游戏调用 GetColorBlindText() 时都会拿到带 <color> 的颜色名。
//
// ⚠ 这个 Prefix 由游戏**自己**按需调用（每次重建名字文本时一次），不是每帧全量，
//   所以保留原地计算是合理的；`ColoredColorName` 内部已有按颜色 id 的缓存。
[HarmonyPatch(typeof(CosmeticsLayer), "GetColorBlindText")]
internal static class AmethystColorBlindTextPatch
{
    private static bool Prefix(CosmeticsLayer __instance, ref string __result)
    {
        try
        {
            if (__instance == null || !AmethystColorBlindText.On) return true;

            int colorId = 0;
            try { colorId = __instance.bodyMatProperties.ColorId; } catch { }

            string name = AmethystColors.ColoredColorName(colorId);
            if (!string.IsNullOrEmpty(name))
            {
                __result = name;
                return false;
            }
        }
        catch { }
        return true;
    }
}