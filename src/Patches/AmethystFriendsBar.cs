using HarmonyLib;
using UnityEngine;

namespace Amethyst.Patches;

internal static class AmethystFriendsBar
{
    
    private static GameObject _friendsButton;
    private static bool _applied;
    private static bool _dumped;      

    internal static bool Enabled => AmethystConfig.MainMenuStyle != null && AmethystConfig.MainMenuStyle.Value;

    internal static void Apply()
    {
        try
        {
            if (!Enabled) return;

            GameObject bar = FindDeep("AccountManager/AccountTab/GameHeader/BarSprite")
                             ?? GameObject.Find("BarSprite");
            if (bar != null)
            {
                var sr = bar.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = Color.clear;
            }

            if (_friendsButton != null)
            {
                if (!_applied) { _applied = true; Log("底板透明 + 角标压暗（已处理）"); }
                return;
            }

            _friendsButton = FindFriendsButton();
            if (_friendsButton == null) return;   

            DimBadge("Highlight", "NewRequestActive");
            DimBadge("Inactive", "NewRequestInactive");

            _applied = true;
            Log("底板透明 + 角标压暗（首次）");
        }
        catch { }
    }

    internal static void TickLive()
    {
        try
        {
            if (!Enabled) return;

            if (!_applied) Apply();

            GameObject btn = _friendsButton != null ? _friendsButton : FindFriendsButton();
            if (btn == null) return;
            _friendsButton = btn;

            PassiveButton pb = btn.GetComponent<PassiveButton>();
            if (pb == null) { DumpOnce(btn, "按钮上没有 PassiveButton"); return; }

            Color accent = ThemeAccent();

            foreach (var sprite in btn.GetComponentsInChildren<SpriteRenderer>(true))
                AmethystUiSpriteTone.Apply(sprite);
            DimBadge("Highlight", "NewRequestActive");
            DimBadge("Inactive", "NewRequestInactive");

            int tinted = 0;
            tinted += TintState(pb.activeSprites, accent, "activeSprites");
            tinted += TintState(pb.inactiveSprites, accent, "inactiveSprites");

            if (tinted == 0)
            {
                tinted += TintNamedChild(btn, "Highlight", accent);
                tinted += TintNamedChild(btn, "Inactive", accent);
            }

            DumpOnce(btn, tinted > 0
                ? $"已染主题色 {HexOf(accent)}：命中 {tinted} 个渲染器（底板精灵保持原版，未换）"
                : "没染上：activeSprites/inactiveSprites 与 Highlight/Inactive.Background 都没命中");
        }
        catch { }
    }

    private static Color ThemeAccent()
    {
        try { return AmethystMainMenuStyle.TierPrimaryColor; }
        catch { return new Color32(0xA8, 0x55, 0xF7, 0xFF); }
    }

    private static int TintState(GameObject state, Color c, string what)
    {
        try
        {
            if (state == null) return 0;
            var sr = state.GetComponent<SpriteRenderer>();
            if (sr == null) return 0;
            
            sr.color = new Color(c.r, c.g, c.b, sr.color.a);
            return 1;
        }
        catch { return 0; }
    }

    private static int TintNamedChild(GameObject root, string child, Color c)
    {
        try
        {
            Transform t = root.transform.FindChild(child);
            if (t == null) return 0;
            int n = 0;
            var srs = t.GetComponentsInChildren<SpriteRenderer>(true);
            if (srs == null) return 0;
            for (int i = 0; i < srs.Length; i++)
            {
                SpriteRenderer sr = srs[i];
                if (sr == null) continue;
                
                bool inBadge = false;
                for (Transform p = sr.transform; p != null && p != t; p = p.parent)
                {
                    if (p.name.StartsWith("NewRequest", System.StringComparison.Ordinal)) { inBadge = true; break; }
                }
                if (inBadge) continue;

                sr.color = new Color(c.r, c.g, c.b, sr.color.a);
                n++;
            }
            return n;
        }
        catch { return 0; }
    }

    private static string HexOf(Color c)
    {
        int r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
        int g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f);
        int b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
        return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
    }

    private static void DumpOnce(GameObject btn, string verdict)
    {
        if (_dumped) return;
        _dumped = true;
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[FriendsBar] 按钮染色：").Append(verdict);
            sb.Append(" | 层级:");
            Walk(btn.transform, sb, 0);
            AmethystPlugin.Logger?.LogInfo(sb.ToString());
        }
        catch { }
    }

    private static void Walk(Transform t, System.Text.StringBuilder sb, int depth)
    {
        if (t == null || depth > 4) return;
        for (int i = 0; i < depth; i++) sb.Append(' ');
        string srTxt;
        try
        {
            var sr = t.GetComponent<SpriteRenderer>();
            srTxt = sr == null ? "-" : HexOf(sr.color) + "/a" + sr.color.a.ToString("F2");
        }
        catch { srTxt = "!"; }
        sb.Append('[').Append(t.name).Append(' ').Append(srTxt).Append(']');
        int n = t.childCount;
        for (int i = 0; i < n; i++)
        {
            try { Walk(t.GetChild(i), sb, depth + 1); }
            catch { }
        }
    }

    internal static GameObject FindFriendsButton()
    {
        try
        {
            return FindDeep("AccountManager/AccountTab/GameHeader/BarSprite/FriendsButton")
                   ?? GameObject.Find("FriendsButton");
        }
        catch { return null; }
    }

    private static GameObject FindDeep(string path)
    {
        try
        {
            
            AccountManager mgr = AccountManager.Instance;
            if (mgr != null && mgr.transform != null)
            {
                Transform t = mgr.transform.Find(path);
                if (t != null) return t.gameObject;

                string trimmed = path.StartsWith("AccountManager/", System.StringComparison.Ordinal)
                    ? path.Substring("AccountManager/".Length)
                    : path;
                t = mgr.transform.Find(trimmed);
                if (t != null) return t.gameObject;
            }

            return GameObject.Find(path);
        }
        catch { return null; }
    }

    private static void DimBadge(string state, string badge)
    {
        try
        {
            Transform t = _friendsButton.transform;
            Transform s = FindDescendant(t, state);
            if (s == null) return;
            Transform b = FindDescendant(s, badge);
            if (b == null) return;

            Color accent = ThemeAccent();

            Transform bg = FindDescendant(b, "Background");
            if (bg != null)
            {
                var sr = bg.gameObject.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(accent.r, accent.g, accent.b, sr.color.a);
            }

            Transform tx = FindDescendant(b, "Text_TMP");
            if (tx != null)
            {
                var tmp = tx.gameObject.GetComponent<TMPro.TextMeshPro>();
                if (tmp != null) tmp.color = Color.white;
            }

            Transform sh = FindDescendant(b, "Shine", "Shrine");
            if (sh != null)
            {
                var sr = sh.gameObject.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = new Color(1f, 1f, 1f, 0.18f);
            }
        }
        catch { }
    }

    private static Transform FindDescendant(Transform root, params string[] names)
    {
        try
        {
            if (root == null) return null;
            int n = root.childCount;
            for (int i = 0; i < n; i++)
            {
                Transform c = root.GetChild(i);
                if (c == null) continue;

                for (int k = 0; k < names.Length; k++)
                {
                    if (string.Equals(c.name, names[k], System.StringComparison.Ordinal)) return c;
                }

                Transform d = FindDescendant(c, names);
                if (d != null) return d;
            }
        }
        catch { }
        return null;
    }

    private static void Log(string what)
    {
        try
        {
            
            bool barOk = FindDeep("AccountManager/AccountTab/GameHeader/BarSprite") != null;
            AmethystPlugin.Logger?.LogInfo(
                $"[FriendsBar] {what}（FinalSuspect AwakeFriendCodeUIPatch 做法）：" +
                $"底板={(barOk ? "已透明" : "未找到")} " +
                $"按钮={(_friendsButton != null ? "已处理" : "未找到")}");
        }
        catch { }
    }
}
