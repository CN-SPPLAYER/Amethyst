using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystGuiHost : MonoBehaviour
{
    private Action[] _draw;

    private void Bind()
    {
        if (_draw != null) return;

        var menu = GetComponent<AmethystMenu>();
        var log = GetComponent<AmethystEventLog>();
        var toast = GetComponent<AmethystToast>();
        var btn = GetComponent<AmethystMenuButton>();
        var recap = GetComponent<AmethystRecapWindow>();
        var music = GetComponent<AmethystMusicPlayer>();
        var update = GetComponent<AmethystUpdateDialog>();

        var order = new List<Action>(5);
        if (menu != null) order.Add(menu.DrawGui);
        if (log != null) order.Add(log.DrawGui);
        if (recap != null) order.Add(recap.DrawGui);
        if (music != null) order.Add(music.DrawGui);
        if (toast != null) order.Add(toast.DrawGui);
        if (btn != null) order.Add(btn.DrawGui);
        if (update != null) order.Add(update.DrawGui);
        
        _draw = order.ToArray();
    }

    public void OnGUI()
    {
    int __p = AmethystPerfProbe.Begin(Section.S_OnGui);
    try { OnGUIInner(); }
    finally { AmethystPerfProbe.End(__p, Section.S_OnGui); }
    }

    public void OnGUIInner()
    {
            Bind();
            if (_draw == null) return;

            Matrix4x4 m = GUI.matrix;
            Color c = GUI.color;

            for (int i = 0; i < _draw.Length; i++)
            {
                try { _draw[i](); }
                catch { }
                GUI.matrix = m;
                GUI.color = c;
            }
    
    }
}
