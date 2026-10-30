using UnityEngine;

namespace Amethyst
{
    // 第 259 轮：不再向系统注册/加载自定义字体。
    //
    // 原做法（释放内嵌 TTF → 写 HKCU 字体表 → Font.CreateDynamicFontFromOSFont）
    // 在部分用户的电脑上会失败（权限/杀毒/字体缓存），导致启动变慢或字体不一致。
    // 现在统一回退游戏自带字体：GetFont 返回 null，ApplyTo 不再改动 GUIStyle。
    internal static class AmethystFont
    {
        internal static Font GetFont() => null;

        internal static void ApplyTo(GUIStyle style)
        {
            // 不应用自定义字体：让 GUIStyle 继续使用 GUI.skin 自带的字体。
        }
    }
}
