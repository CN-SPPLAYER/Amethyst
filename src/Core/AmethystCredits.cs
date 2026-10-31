namespace Amethyst;

internal sealed class CreditGroup
{
    internal string TitleKey = string.Empty;   
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
