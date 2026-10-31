using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace Amethyst;

internal static class AmethystNameDiag
{
    private static float _next;

    internal static void Tick(List<AmethystPlayerCache> list, bool mixup)
    {
        try
        {
            if (!AmethystPerfProbe.On) return;

            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + 1f;

            var sb = new StringBuilder(512);
            sb.Append("[NameDiag] switch=").Append(AmethystColors.NameColorsOn)
              .Append(" mixup=").Append(mixup)
              .Append(" localId=").Append(AmethystPlayerCacheStore.LocalId)
              .Append(" inLobby=").Append(AmethystPlayerCacheStore.InLobby)
              .Append(" inGame=").Append(AmethystPlayerCacheStore.InGame)
              .Append(" n=").Append(list == null ? -1 : list.Count);

            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    AmethystPlayerCache c = list[i];
                    if (c == null) { sb.Append(" | null"); continue; }

                    sb.Append("\n  #").Append(c.Id)
                      .Append(" pc=").Append(c.Pc == null ? "X" : "o")
                      .Append(" tmp=").Append(c.NameText == null ? "X" : "o")
                      .Append(" dirty=").Append(c.Dirty ? 1 : 0)
                      .Append(" missData=").Append(c.DataMissingAttempts)
                      .Append(" shift=").Append(c.IsShapeshifting ? 1 : 0)
                      .Append(" outDef=").Append(c.OutfitIsDefault ? 1 : 0)
                      .Append(" name=[").Append(Trunc(c.Name, 12)).Append(']')
                      .Append(" pend=").Append(c.PendingNameText == null ? "X" : "o")
                      .Append(" wroteSameObj=").Append(ReferenceEquals(c.WrittenForName, c.NameText) ? 1 : 0)
                      .Append(" written=[").Append(Trunc(c.WrittenNameText, 14)).Append(']')
                      .Append(" colorId=").Append(c.ColorId);

                    TextMeshPro t = c.NameText;
                    if (t != null)
                    {
                        try
                        {
                            sb.Append(" live=[").Append(Trunc(t.text, 14)).Append(']')
                              .Append(" en=").Append(t.enabled ? 1 : 0)
                              .Append(" col=").Append(t.color.r.ToString("F2"))
                              .Append('/').Append(t.color.g.ToString("F2"))
                              .Append('/').Append(t.color.b.ToString("F2"))
                              .Append('/').Append(t.color.a.ToString("F2"))
                              .Append(" go=").Append(t.gameObject.activeInHierarchy ? 1 : 0);
                        }
                        catch { sb.Append(" live=<read-fail>"); }
                    }
                }
            }

            AmethystPlugin.Logger?.LogInfo(sb.ToString());
        }
        catch { }
    }

    private static string Trunc(string s, int n)
    {
        if (s == null) return "null";
        s = s.Replace("\n", "\\n");
        return s.Length <= n ? s : s.Substring(0, n) + "..";
    }
}
