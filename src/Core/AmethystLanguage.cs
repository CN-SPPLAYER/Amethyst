using System;

namespace Amethyst;

// ===== 游戏语言探测（第 273 轮）=====
//
// 需求：首次启动时模组语言跟随游戏语言，而不是固定中文；
//       用户之后在菜单里改过语言，就以用户的选择为准。
//
// 实现方式：配置项 `General.Language` 的默认值改为 `auto`。
//   · 值为 `auto` 时，从 `TranslationController.Instance.currentLanguage.languageID`
//     读取游戏语言并映射到模组支持的三种语言，成功后把结果写回配置；
//   · 写回之后配置里就是 zh / en / ru，后续启动直接使用该值；
//   · 用户在菜单切换语言时，ConfigEntry 被直接改写，自然覆盖自动结果。
internal static class AmethystLanguage
{
    internal const string Auto = "auto";

    // 返回 "zh" / "ru" / "en"；游戏尚未初始化时返回空字符串（调用方不要写配置）
    internal static string Detect()
    {
        try
        {
            TranslationController tc = TranslationController.Instance;
            if (tc == null) return string.Empty;

            LanguageUnit unit = tc.currentLanguage;
            if (unit == null) return string.Empty;

            return Map(unit.languageID);
        }
        catch { return string.Empty; }
    }

    // TranslationController 是否已经创建（用于区分「游戏还没初始化」与「读取失败」）
    internal static bool ControllerReady()
    {
        try { return TranslationController.Instance != null; }
        catch { return false; }
    }

    private static string Map(SupportedLangs lang)
    {
        switch (lang)
        {
            case SupportedLangs.SChinese:
            case SupportedLangs.TChinese:
                return "zh";

            case SupportedLangs.Russian:
                return "ru";

            // 模组目前只提供中/英/俄三语，其余语言统一回退英文
            default:
                return "en";
        }
    }
}
