using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace Amethyst;

// 文字高光：一条亮带从**左往右**扫过模组文字。
//
// 参考 OnyxMenu 的 `NocturneNameColor`：不依赖 TMP 顶点色，
// 而是每帧把文字重写成「逐字符 `<color>` 标签」的富文本。
// 这是本游戏里最稳定的做法（顶点色在 IL2CPP/TMP 下有时不生效）。
public sealed class AmethystTextShine : MonoBehaviour
{
    public AmethystTextShine(IntPtr ptr) : base(ptr) { }

    private TMP_Text _text;
    private string _prefix = string.Empty;
    private string _tag = string.Empty;
    private bool _ready;

    // 基础色（品牌紫）与高光色（白）。
    private static readonly Color BaseColor = new Color(0.71f, 0.33f, 1f, 1f);
    private static readonly Color HighlightColor = Color.white;

    private const float Speed = 9f;    // 扫过速度
    private const float Band = 2.5f;   // 亮带宽度（字符数）

    private readonly StringBuilder _sb = new StringBuilder(128);
    private float _next;

    internal void Setup(TMP_Text text, string prefix, string tag)
    {
        _text = text;
        _prefix = prefix ?? string.Empty;
        _tag = tag ?? string.Empty;
        _ready = true;
    }

    private void LateUpdate()
    {
        if (!_ready || _text == null || _tag.Length == 0) return;
        // 扫光不需要 60fps：限到约 30fps，省掉一半的富文本重建与 TMP 重排。
        float now = Time.unscaledTime;
        if (now < _next) return;
        _next = now + 1f / 30f;
        try
        {
            int n = _tag.Length;
            float head = Mathf.Repeat(Time.unscaledTime * Speed, n + Band * 2f) - Band;

            _sb.Length = 0;
            _sb.Append(_prefix).Append("<b>");
            for (int i = 0; i < n; i++)
            {
                char ch = _tag[i];
                if (ch == ' ') { _sb.Append(' '); continue; }

                float d = Mathf.Abs(i - head);
                float g = Mathf.Clamp01(1f - d / Band);
                g *= g;
                Color c = Color.Lerp(BaseColor, HighlightColor, g);
                _sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>').Append(ch).Append("</color>");
            }
            _sb.Append("</b>");

            string s = _sb.ToString();
            if (_text.text != s) _text.text = s;
        }
        catch { }
    }
}
