namespace Amethyst;

// ===== 制作人员名单（Credits）=====
//
// 按需求：**直接硬编码在代码里**，不依赖任何外部文件或配置项。
// 内容来源：用户提供的《制作人员名单.txt》：
//   编程：一只屑小紫
//   测试人员：微生猫、完了，没豆了
//   捐赠人员：完了，没豆了
// 分类标题用语言键（三语），人名保持原样。
//
// 后续增补：
//   Elauk → 「编程」（按用户要求追加，不在原始《制作人员名单.txt》里）。
// 名单条数与卡片高度都是按 `Members.Length` 现算的，加人不用改别的地方。

internal sealed class CreditGroup
{
    internal string TitleKey = string.Empty;   // 语言键，如 CreditsProgramming
    internal string[] Members = new string[0];
}

internal static class AmethystCredits
{
    internal static readonly CreditGroup[] Groups =
    {
        new CreditGroup
        {
            TitleKey = "CreditsProgramming",
            Members = new[] { "一只屑小紫", "Elauk" },
        },
        new CreditGroup
        {
            TitleKey = "CreditsTesters",
            Members = new[] { "微生猫", "完了，没豆了" },
        },
        new CreditGroup
        {
            TitleKey = "CreditsDonors",
            Members = new[] { "完了，没豆了", "海王星Neptune" },
        },
    };

    // 名单总人数（含重复出现的人，按条目计）
    internal static int Count
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Groups.Length; i++)
                if (Groups[i].Members != null) n += Groups[i].Members.Length;
            return n;
        }
    }
}
