using System;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystUpdateDialog : MonoBehaviour
{
    public AmethystUpdateDialog(IntPtr ptr) : base(ptr) { }

    private GUIStyle _title;
    private GUIStyle _body;
    private GUIStyle _btnLabel;
    private GUIStyle _invisible;
    private bool _built;

    internal static bool Visible =>
        AmethystUpdater.NeedConfirm
        || AmethystUpdater.NeedRestart
        || AmethystUpdater.State == AmethystUpdater.Phase.Downloading
        || AmethystUpdater.State == AmethystUpdater.Phase.Failed;

    internal static void DismissFailure()
    {
        if (AmethystUpdater.State == AmethystUpdater.Phase.Failed) AmethystUpdater.Reset();
    }

    [HideFromIl2Cpp]
    internal void DrawGui()
    {
        if (!Visible) return;
        try { DrawInner(); } catch { }
    }

    [HideFromIl2Cpp]
    private void Build()
    {
        if (_built) return;
        _built = true;

        AmethystPalette p = AmethystStyle.Current;

        _invisible = new GUIStyle();

        _title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            richText = true,
        };
        _title.normal.textColor = p.Text;
        _title.padding = AmethystStyle.Offset(0, 0, 0, 0);

        _body = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.UpperLeft,
            richText = true,
            wordWrap = true,
        };
        _body.normal.textColor = p.Muted;
        _body.padding = AmethystStyle.Offset(0, 0, 0, 0);

        _btnLabel = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Normal,
            alignment = TextAnchor.MiddleCenter,
            richText = true,
        };
        _btnLabel.normal.textColor = p.Text;
    }

    [HideFromIl2Cpp]
    private bool Button(Rect r, string label, bool primary, AmethystPalette p)
    {
        bool hover = r.Contains(Event.current != null ? Event.current.mousePosition : Vector2.zero);

        Color bg = primary
            ? (hover ? A(p.Accent, 0.95f) : A(p.Accent, 0.80f))
            : (hover ? A(Color.white, 0.12f) : A(Color.white, 0.06f));

        AmethystStyle.FillRounded(r, bg, 8);
        AmethystStyle.StrokeRounded(r, primary ? A(p.Accent, 0.95f) : A(Color.white, 0.12f), 8, 1);

        Color prev = _btnLabel.normal.textColor;
        _btnLabel.normal.textColor = primary ? Color.white : p.Text;
        GUI.Label(r, label, _btnLabel);
        _btnLabel.normal.textColor = prev;

        return GUI.Button(r, GUIContent.none, _invisible);
    }

    private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

    [HideFromIl2Cpp]
    private void DrawInner()
    {
        Build();
        if (_title == null) return;

        AmethystPalette p = AmethystStyle.Current;

        int prevDepth = GUI.depth;
        Color prevColor = GUI.color;
        GUI.depth = -20000;   
        GUI.color = Color.white;

        try
        {
            float w = Mathf.Min(470f, Screen.width - 40f);
            float h = 215f;
            var card = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);

            AmethystStyle.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));

            AmethystStyle.FillRounded(card, p.Panel, 12);
            AmethystStyle.StrokeRounded(card, A(Color.white, 0.10f), 12, 1);

            float pad = 22f;
            float x = card.x + pad;
            float cw = card.width - pad * 2f;
            float y = card.y + 18f;

            GUI.Label(new Rect(x, y, cw, 26f), AmethystText.T("CheckUpdate"), _title);
            y += 34f;

            string body;
            string primaryLabel = null;

            switch (AmethystUpdater.State)
            {
                case AmethystUpdater.Phase.Found:
                    body = AmethystText.T("UpdateConfirm",
                        AmethystPlugin.PluginVersion, AmethystUpdater.RemoteVersion);
                    primaryLabel = AmethystText.T("UpdateDownloadNow");
                    break;

                case AmethystUpdater.Phase.Ready:
                    body = AmethystText.T("UpdateReady", AmethystUpdater.RemoteVersion)
                        + "\n" + AmethystText.T("UpdateApplyHint");
                    primaryLabel = AmethystText.T("UpdateApplyNow");
                    break;

                case AmethystUpdater.Phase.Downloading:
                    body = AmethystText.T("UpdateDownloading",
                        Mathf.RoundToInt(Mathf.Clamp01(AmethystUpdater.Progress) * 100f));
                    break;

                case AmethystUpdater.Phase.Failed:
                    body = AmethystText.T("UpdateFailedBody", AmethystUpdater.Error ?? string.Empty);
                    break;

                default:
                    body = AmethystText.T("UpdateUpToDate", AmethystPlugin.PluginVersion);
                    break;
            }

            GUI.Label(new Rect(x, y, cw, 76f), body, _body);
            y += 80f;

            if (AmethystUpdater.State == AmethystUpdater.Phase.Downloading)
            {
                var bar = new Rect(x, y, cw, 6f);
                AmethystStyle.FillRounded(bar, A(Color.white, 0.08f), 3);
                float pw = cw * Mathf.Clamp01(AmethystUpdater.Progress);
                if (pw > 1f) AmethystStyle.FillRounded(new Rect(x, y, pw, 6f), p.Accent, 3);
            }

            float bw = 108f;
            float bh = 32f;
            float by = card.y + card.height - bh - 18f;
            var laterR = new Rect(card.x + card.width - pad - bw, by, bw, bh);
            var primaryR = new Rect(laterR.x - bw - 10f, by, bw, bh);

            bool primaryClicked = false;
            if (primaryLabel != null) primaryClicked = Button(primaryR, primaryLabel, true, p);

            bool laterClicked = Button(laterR, AmethystText.T("UpdateLater"), false, p);
            if (laterClicked)
            {
                
                if (AmethystUpdater.State == AmethystUpdater.Phase.Found) AmethystUpdater.NeedConfirm = false;
                else if (AmethystUpdater.State == AmethystUpdater.Phase.Failed) AmethystUpdater.Reset();
                else AmethystUpdater.NeedRestart = false;
            }

            if (primaryClicked)
            {
                if (AmethystUpdater.State == AmethystUpdater.Phase.Found)
                {
                    AmethystUpdater.NeedConfirm = false;
                    try { AmethystUpdaterHost.Instance?.BeginDownload(); } catch { }
                }
                else if (AmethystUpdater.State == AmethystUpdater.Phase.Ready)
                {
                    AmethystUpdater.BeginReplace();
                }
            }
        }
        finally
        {
            GUI.depth = prevDepth;
            GUI.color = prevColor;
        }
    }
}
