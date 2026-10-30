using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

[HarmonyPatch]
internal static class AmethystMainMenuButtons
{
    private static PassiveButton Template;
    private static PassiveButton GitHubButton;
    private static PassiveButton QQGroupButton;
    private static PassiveButton ReportButton;
    private static PassiveButton UpdateButton;

    // 「举报作弊者」跳转的外部举报页。写死常量而不是内联到 lambda 里：
    // 点击时要同时写剪贴板 + 开浏览器，两处必须用同一个字符串，避免日后改一半。
    private const string ReportUrl = "https://api2.elauk.top/acban/submit";

    // 四个按钮共用同一行（y 相同）、同一固定间距，新增按钮只要接在右边一格。
    private const float RowY = -1.3f;
    private const float ButtonSpacing = 1.8f;
    private const float GitHubX = -2.3f;
    private const float QQGroupX = -0.5f;
    private const float ReportX = QQGroupX + ButtonSpacing;  // = 1.3
    private const float UpdateX = ReportX + ButtonSpacing;   // = 3.1，接在「举报作弊者」右侧一格

    internal static bool Enabled => AmethystConfig.MainMenuButtons == null || AmethystConfig.MainMenuButtons.Value;

    [HarmonyPatch(typeof(MainMenuManager), "Start")]
    [HarmonyPrefix]
    private static void Start_Prefix(MainMenuManager __instance)
    {
        if (!Template) Template = __instance.quitButton;
        // 第 221 轮：顺手给「复盘显示/隐藏」按钮抓模板（它要用原生 PassiveButton 克隆体，见 AmethystRecapButton）。
        AmethystRecapButton.SetBase(Template);
    }

    [HarmonyPatch(typeof(MainMenuManager), "Start")]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.VeryHigh)]
    private static void Start_Postfix(MainMenuManager __instance)
    {
        if (!Template) Template = __instance.quitButton;
        if (!Template) return;

        if (!Enabled)
        {
            if (GitHubButton != null && GitHubButton.gameObject != null) GitHubButton.gameObject.SetActive(false);
            if (QQGroupButton != null && QQGroupButton.gameObject != null) QQGroupButton.gameObject.SetActive(false);
            if (ReportButton != null && ReportButton.gameObject != null) ReportButton.gameObject.SetActive(false);
            if (UpdateButton != null && UpdateButton.gameObject != null) UpdateButton.gameObject.SetActive(false);
            return;
        }

        // GitHub 按钮
        if (!GitHubButton || !GitHubButton.gameObject)
        {
            GitHubButton = CreateButton(
                "GitHubButton",
                new Vector3(GitHubX, RowY, 1f),
                new Color32(153, 153, 153, 255),
                new Color32(209, 209, 209, 255),
                () =>
                {
                    try { GUIUtility.systemCopyBuffer = "https://github.com/CN-SPPLAYER/Amethyst"; } catch { }
                    try { Application.OpenURL("https://github.com/CN-SPPLAYER/Amethyst"); } catch { }
                    AmethystToast.Push("GitHub", AmethystText.T("CopiedToClipboard"), 3.0f, AmethystNotifyKind.Info);
                },
                AmethystText.T("GitHub"));
        }
        if (GitHubButton != null)
        {
            GitHubButton.gameObject.SetActive(Enabled);
            UpdateLabel(GitHubButton, AmethystText.T("GitHub"));
        }

        // QQ群 按钮
        if (!QQGroupButton || !QQGroupButton.gameObject)
        {
            QQGroupButton = CreateButton(
                "QQGroupButton",
                new Vector3(QQGroupX, RowY, 1f),
                new Color32(88, 101, 242, 255),
                new Color32(148, 161, 255, 255),
                () =>
                {
                    try { GUIUtility.systemCopyBuffer = "1031096791"; } catch { }
                    AmethystToast.Push(AmethystText.T("QQGroup"), "1031096791 (" + AmethystText.T("CopiedToClipboard") + ")", 3.0f, AmethystNotifyKind.Info);
                },
                AmethystText.T("QQGroup"));
        }
        if (QQGroupButton != null)
        {
            QQGroupButton.gameObject.SetActive(Enabled);
            UpdateLabel(QQGroupButton, AmethystText.T("QQGroup"));
        }

        // 举报作弊者 按钮（「QQ群」按钮右侧一格；点一下直接把浏览器带到举报页）
        if (!ReportButton || !ReportButton.gameObject)
        {
            ReportButton = CreateButton(
                "ReportButton",
                new Vector3(ReportX, RowY, 1f),
                new Color32(196, 60, 60, 255),
                new Color32(240, 110, 110, 255),
                () =>
                {
                    // 先写剪贴板再开浏览器：OpenURL 在某些环境（无默认浏览器 / 被安全软件拦）
                    // 会静默失败，留一份链接在剪贴板里用户还能手动粘。与 GitHub 按钮同一套路。
                    try { GUIUtility.systemCopyBuffer = ReportUrl; } catch { }
                    try { Application.OpenURL(ReportUrl); } catch { }
                    AmethystToast.Push(AmethystText.T("ReportCheater"), AmethystText.T("CopiedToClipboard"), 3.0f, AmethystNotifyKind.Info);
                },
                AmethystText.T("ReportCheater"));
            EnableAutoShrink(ReportButton);
        }
        if (ReportButton != null)
        {
            ReportButton.gameObject.SetActive(Enabled);
            UpdateLabel(ReportButton, AmethystText.T("ReportCheater"));
        }

        // 检查更新 按钮（「举报作弊者」右侧一格）
        // Android/Starlight 上没有 cmd.exe 与重启流程，自动更新不可用，因此不创建该按钮。
        bool showUpdate = !AmethystPlatform.IsAndroid;
        if (showUpdate && (!UpdateButton || !UpdateButton.gameObject))
        {
            UpdateButton = CreateButton(
                "UpdateButton",
                new Vector3(UpdateX, RowY, 1f),
                new Color32(52, 140, 92, 255),
                new Color32(96, 200, 140, 255),
                () =>
                {
                    if (AmethystUpdater.Busy)
                    {
                        AmethystToast.Push(AmethystText.T("CheckUpdate"),
                            AmethystText.T("UpdateBusy"), 3f, AmethystNotifyKind.Info);
                        return;
                    }
                    try { AmethystUpdaterHost.Instance?.BeginCheck(); }
                    catch { }
                    AmethystToast.Push(AmethystText.T("CheckUpdate"),
                        AmethystText.T("UpdateChecking"), 2.5f, AmethystNotifyKind.Info);
                },
                AmethystText.T("CheckUpdate"));
            EnableAutoShrink(UpdateButton);
        }
        if (UpdateButton != null)
        {
            UpdateButton.gameObject.SetActive(Enabled && showUpdate);
            UpdateLabel(UpdateButton, AmethystText.T("CheckUpdate"));
        }
    }

    internal static void TickLive()
    {
        try
        {
            if (GitHubButton != null && GitHubButton.gameObject != null)
                GitHubButton.gameObject.SetActive(Enabled);
            if (QQGroupButton != null && QQGroupButton.gameObject != null)
                QQGroupButton.gameObject.SetActive(Enabled);
            if (ReportButton != null && ReportButton.gameObject != null)
                ReportButton.gameObject.SetActive(Enabled);
            if (UpdateButton != null && UpdateButton.gameObject != null)
                UpdateButton.gameObject.SetActive(Enabled && !AmethystPlatform.IsAndroid);
        }
        catch { }
    }

    // 「举报作弊者」是这一行里唯一的长标签：5 个汉字在 TMP 里约占 5 em，
    // 而相邻的 "GitHub"（6 个西文字符）只占约 3 em —— 照抄 3.5 号定长字会横向顶出按钮底板。
    // 这里只给这一个按钮开 TMP 自动缩字号：放得下就还是 3.5（与另外两个按钮一致），
    // 放不下才逐级降到 fontSizeMin，保证文字始终留在按钮内、也不会去压旁边的按钮。
    private static void EnableAutoShrink(PassiveButton button, float min = 1.5f, float max = 3.5f)
    {
        var text = button?.transform.Find("FontPlacer/Text_TMP")?.GetComponent<TMP_Text>();
        if (text == null) return;
        text.fontSizeMax = max;
        text.fontSizeMin = min;
        text.enableAutoSizing = true;
    }

    private static void UpdateLabel(PassiveButton button, string label)
    {
        var buttonText = button?.transform.Find("FontPlacer/Text_TMP")?.GetComponent<TMP_Text>();
        if (buttonText != null && buttonText.text != label)
        {
            buttonText.text = label;
        }
    }

    private static PassiveButton CreateButton(string name, Vector3 localPosition, Color32 normalColor, Color32 hoverColor, Action action, string label, Vector2? scale = null)
    {
        PassiveButton button = UnityEngine.Object.Instantiate(Template, Template.transform.parent);
        button.name = name;
        var aspect = button.GetComponent<AspectPosition>();
        if (aspect != null) UnityEngine.Object.Destroy(aspect);
        button.transform.localPosition = localPosition;

        button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.OnClick.AddListener((UnityEngine.Events.UnityAction)action);

        var buttonText = button.transform.Find("FontPlacer/Text_TMP")?.GetComponent<TMP_Text>();
        if (buttonText != null)
        {
            var translator = buttonText.GetComponent<TextTranslatorTMP>();
            if (translator != null) UnityEngine.Object.Destroy(translator);

            buttonText.fontSize = buttonText.fontSizeMax = buttonText.fontSizeMin = 3.5f;
            buttonText.enableWordWrapping = false;
            buttonText.text = label;

            Transform container = buttonText.transform.parent;
            if (container != null)
            {
                var aspectContainer = container.GetComponent<AspectPosition>();
                if (aspectContainer != null) UnityEngine.Object.Destroy(aspectContainer);
                Vector3 cPos = container.localPosition;
                container.localPosition = new Vector3(0f, cPos.y, cPos.z);
            }

            var aspectText = buttonText.GetComponent<AspectPosition>();
            if (aspectText != null) UnityEngine.Object.Destroy(aspectText);
            Vector3 tPos = buttonText.transform.localPosition;
            buttonText.transform.localPosition = new Vector3(0f, tPos.y, tPos.z);
            buttonText.horizontalAlignment = HorizontalAlignmentOptions.Center;
        }

        var normalSprite = button.inactiveSprites?.GetComponent<SpriteRenderer>();
        var hoverSprite = button.activeSprites?.GetComponent<SpriteRenderer>();
        if (normalSprite != null) normalSprite.color = normalColor;
        if (hoverSprite != null) hoverSprite.color = hoverColor;

        var buttonCollider = button.GetComponent<BoxCollider2D>();
        if (buttonCollider != null && scale.HasValue && normalSprite != null && hoverSprite != null)
        {
            normalSprite.size = hoverSprite.size = buttonCollider.size = scale.Value;
        }
        if (buttonCollider != null)
        {
            buttonCollider.offset = new Vector2(0f, 0f);
        }

        return button;
    }
}
