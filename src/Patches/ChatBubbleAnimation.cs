using System;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Amethyst.Patches;

internal static class ChatBubbleAnimations
{
    private const int Reach = 12;
    private const int OpenMax = 9;
    private const float ViewH = 5f;
    private const float GlideRate = 13f;
    private const float Span = 0.62f;

    private struct Look
    {
        internal float Slide;
        internal float Rise;
        internal float Dip;
        internal float Pop;
        internal float Speed;
        internal float Flash;

        internal float Steps;

        internal Look(float slide, float rise, float dip, float pop, float speed, float flash, float steps)
        {
            Slide = slide;
            Rise = rise;
            Dip = dip;
            Pop = pop;
            Speed = speed;
            Flash = flash;
            Steps = steps;
        }
    }

    private static readonly Look Others = new Look(0.30f, 0.16f, 0.06f, 0.35f, 1f, 0f, 8f);
    private static readonly Look Mine = new Look(0.10f, 0.30f, 0.05f, 0.45f, 1.15f, 0f, 8f);
    private static readonly Look Calm = new Look(0.14f, 0.10f, 0.025f, 0.6f, 1.4f, 0f, 4f);

    private sealed class Entry
    {
        internal float Start;
        internal float Side;
        internal Look Style;
        internal Transform Root;
        internal Vector3 RootScale;
        internal Transform Avatar;
        internal Vector3 AvatarScale;
        internal SpriteRenderer Bg;
        internal Color BgColor;
        internal Color FlashColor;
        internal TMP_Text NameText;
        internal Color NameColor;
        internal float NameLast;
        internal Transform NameTr;
        internal Vector3 NamePos;
        internal TMP_Text MsgText;
        internal Color MsgColor;
        internal float MsgLast;
        internal Transform MsgTr;
        internal Vector3 MsgPos;
        internal bool BgDone;
        internal bool NameDone;
        internal bool MsgDone;
    }

    private sealed class Track
    {
        internal Transform T;
        internal float X;
        internal float Y;
        internal float Z;
        internal float Glide;
        internal float EnterX;
        internal float EnterY;
        internal float Hold;
        internal bool Fresh;
        internal int Stamp;
        internal Entry Enter;
    }

    private static int stamp;
    private static int unread;
    private static float sync = -1f;
    private static readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();
    private static readonly List<int> gone = new List<int>();
    private static readonly List<Transform> seen = new List<Transform>();
    private static readonly List<int> seenId = new List<int>();
    private static readonly List<float> seenY = new List<float>();

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    private static float Back(float t, float c)
    {
        float u = Mathf.Clamp01(t) - 1f;
        return 1f + (c + 1f) * u * u * u + c * u * u;
    }

    private static float Gait()
    {
        return Mathf.Clamp(AmethystConfig.BubbleAnimSpeed.Value, 0.5f, 2.0f);
    }

    private static Color ReadColor(TMP_Text text)
    {
        return text != null ? ((Graphic)text).color : Color.white;
    }

    private static void WriteColor(TMP_Text text, Color color)
    {
        if (text != null)
        {
            ((Graphic)text).color = color;
        }
    }

    private static SpriteRenderer BubbleBackground(ChatBubble bubble)
    {
        SpriteRenderer renderer = bubble.Background;
        if (renderer != null)
        {
            return renderer;
        }

        Transform found = ((Component)bubble).transform.Find("Background");
        return found != null ? ((Component)found).GetComponent<SpriteRenderer>() : null;
    }

    internal static void Register(ChatBubble bubble)
    {
        if (bubble == null || !AmethystConfig.BubbleAnim.Value)
        {
            return;
        }

        if (((Component)bubble).gameObject.activeInHierarchy)
        {
            Begin(bubble, Time.unscaledTime, 0);
        }
        else if (unread < 40)
        {
            unread++;
        }
    }

    private static float OpenSync(ChatController chat)
    {
        if (sync >= 0f)
        {
            return sync;
        }

        float t = 0.02f;
        AnimationCurve curve = chat.chatAnimationIn;
        if (curve != null)
        {
            while (t < 0.5f && curve.Evaluate(t) < 0.55f)
            {
                t += 0.02f;
            }
        }

        sync = t * 0.8f;
        return sync;
    }

    internal static void Cascade(ChatController chat)
    {
        int pending = unread;
        unread = 0;
        if (chat == null || !AmethystConfig.BubbleAnim.Value || !AmethystConfig.BubbleCascade.Value)
        {
            return;
        }

        if (chat.chatBubblePool == null || chat.scroller == null || chat.scroller.Inner == null)
        {
            return;
        }

        try
        {
            var list = chat.chatBubblePool.activeChildren;
            float low = -1.85f - chat.scroller.Inner.localPosition.y - 0.4f;
            float high = low + ViewH;
            float start = Time.unscaledTime + OpenSync(chat);
            int shown = 0;
            for (int i = list.Count - 1; i >= 0 && shown < OpenMax; i--)
            {
                PoolableBehavior pooled = list[i];
                if (pooled == null)
                {
                    continue;
                }

                float y = ((Component)pooled).transform.localPosition.y;
                if (y > high)
                {
                    break;
                }

                if (y < low)
                {
                    continue;
                }

                ChatBubble bubble = pooled.TryCast<ChatBubble>();
                if (bubble == null || !((Component)bubble).gameObject.activeInHierarchy)
                {
                    continue;
                }

                Begin(bubble, start + shown * 0.032f, list.Count - 1 - i < pending ? 1 : 2);
                shown++;
            }
        }
        catch
        {
        }
    }

    private static void Begin(ChatBubble bubble, float start, int mode)
    {
        Transform root = ((Component)bubble).transform;
        int id = root.GetInstanceID();
        tracks.TryGetValue(id, out Track track);
        bool fresh = mode == 0;
        if (!fresh && track != null && track.Enter != null)
        {
            return;
        }

        if (track != null && track.Enter != null)
        {
            
            Release(track);
        }

        Transform avatar = bubble.Player != null ? ((Component)bubble.Player).transform : null;
        float side = avatar != null && avatar.localPosition.x > 2f ? 1f : -1f;
        Look look = side > 0f ? Mine : Others;
        if (mode == 2 || (fresh && Entering() >= 3))
        {
            look = Calm;
        }

        try
        {
            ChatThemeStyler.ApplyBubble(bubble);
        }
        catch (Exception error)
        {
            AmethystPlugin.Logger?.LogWarning("[Bubble] 主题重申失败: " + error.Message);
        }

        Vector3 pos = root.localPosition;
        SpriteRenderer bg = BubbleBackground(bubble);
        TMP_Text nameText = bubble.NameText != null ? (TMP_Text)bubble.NameText : null;
        TMP_Text msgText = bubble.TextArea != null ? (TMP_Text)bubble.TextArea : null;

        Entry entry = new Entry
        {
            Start = start,
            Side = side,
            Style = look,
            Root = root,
            RootScale = root.localScale,
            Avatar = avatar,
            AvatarScale = avatar != null ? avatar.localScale : Vector3.one,
            Bg = bg,
            BgColor = bg != null ? bg.color : Color.white,
            FlashColor = Color.Lerp(AmethystStyle.Current.Accent, Color.white, 0.15f),
            NameText = nameText,
            NameColor = ReadColor(nameText),
            NameLast = -1f,
            NameTr = nameText != null ? nameText.transform : null,
            MsgText = msgText,
            MsgColor = ReadColor(msgText),
            MsgLast = -1f,
            MsgTr = msgText != null ? msgText.transform : null,
        };
        if (entry.NameTr != null)
        {
            entry.NamePos = entry.NameTr.localPosition;
        }

        if (entry.MsgTr != null)
        {
            entry.MsgPos = entry.MsgTr.localPosition;
        }

        if (track == null || fresh)
        {
            track = new Track { T = root, X = pos.x, Y = pos.y, Z = pos.z, Fresh = fresh };
            tracks[id] = track;
        }

        track.Enter = entry;
        Animate(track, entry, Time.unscaledTime - start);
        Place(track);
    }

    private static int Entering()
    {
        int count = 0;
        foreach (KeyValuePair<int, Track> pair in tracks)
        {
            if (pair.Value.Enter != null)
            {
                count++;
            }
        }

        return count;
    }

    internal static void Before(ChatController chat)
    {
        seen.Clear();
        seenId.Clear();
        seenY.Clear();
        if (chat == null || !AmethystConfig.BubbleAnim.Value || !AmethystConfig.BubbleReflow.Value)
        {
            return;
        }

        try
        {
            if (chat.State == ChatControllerState.Closed)
            {
                return;
            }

            var list = chat.chatBubblePool.activeChildren;
            if (list == null)
            {
                return;
            }

            for (int i = Mathf.Max(0, list.Count - Reach); i < list.Count; i++)
            {
                PoolableBehavior pooled = list[i];
                if (pooled == null)
                {
                    continue;
                }

                Transform transform = ((Component)pooled).transform;
                int id = transform.GetInstanceID();
                if (tracks.TryGetValue(id, out Track track) && track.Fresh)
                {
                    continue;
                }

                seen.Add(transform);
                seenId.Add(id);
                seenY.Add(transform.localPosition.y);
            }
        }
        catch
        {
            seen.Clear();
            seenId.Clear();
            seenY.Clear();
        }
    }

    internal static void ClearSeen()
    {
        seen.Clear();
        seenId.Clear();
        seenY.Clear();
    }

    internal static void After()
    {
        stamp++;
        gone.Clear();
        for (int i = 0; i < seen.Count; i++)
        {
            Transform transform = seen[i];
            if (transform == null)
            {
                continue;
            }

            Vector3 pos = transform.localPosition;
            float jump = seenY[i] - pos.y;
            bool moved = Mathf.Abs(jump) >= 0.002f;
            if (!tracks.TryGetValue(seenId[i], out Track track))
            {
                if (!moved)
                {
                    continue;
                }

                track = new Track { T = transform, X = pos.x, Y = pos.y, Z = pos.z };
                tracks[seenId[i]] = track;
            }

            track.Y = pos.y;
            
            track.X = pos.x;
            track.Z = pos.z;
            track.Glide = moved ? jump - track.EnterY : 0f;
            track.Hold = moved ? Mathf.Min(0.1f, (seen.Count - 1 - i) * 0.018f) : 0f;
            track.Stamp = stamp;
        }

        foreach (KeyValuePair<int, Track> pair in tracks)
        {
            Track track = pair.Value;
            if (track.T == null)
            {
                gone.Add(pair.Key);
                continue;
            }

            if (track.Stamp != stamp)
            {
                if (!track.T.gameObject.activeInHierarchy)
                {
                    Release(track);
                    gone.Add(pair.Key);
                    continue;
                }

                track.Y = track.T.localPosition.y;
                track.Glide = 0f;
            }

            track.Fresh = false;
            Place(track);
        }

        for (int i = 0; i < gone.Count; i++)
        {
            tracks.Remove(gone[i]);
        }

        seen.Clear();
        seenId.Clear();
        seenY.Clear();
    }

    internal static void Tick(ChatController chat)
    {
        if (tracks.Count == 0)
        {
            return;
        }

        if (chat == null || !AmethystConfig.BubbleAnim.Value)
        {
            ReleaseAll();
            return;
        }

        if (chat.State == ChatControllerState.Closed)
        {
            ReleaseAll();
            return;
        }

        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        float decay = Mathf.Exp(-GlideRate * dt);
        gone.Clear();
        foreach (KeyValuePair<int, Track> pair in tracks)
        {
            Track track = pair.Value;
            try
            {
                if (track.T == null || !Step(track, now, dt, decay))
                {
                    gone.Add(pair.Key);
                }
            }
            catch
            {
                
                gone.Add(pair.Key);
                try { Release(track); } catch { }
            }
        }

        for (int i = 0; i < gone.Count; i++)
        {
            tracks.Remove(gone[i]);
        }
    }

    private static void ReleaseAll()
    {
        foreach (KeyValuePair<int, Track> pair in tracks)
        {
            Release(pair.Value);
        }

        tracks.Clear();
    }

    private static bool Step(Track track, float now, float dt, float decay)
    {
        track.Fresh = false;
        Entry entry = track.Enter;
        if (entry != null)
        {
            float t = now - entry.Start;
            if (t * entry.Style.Speed * Gait() >= Span)
            {
                Finish(entry);
                track.Enter = null;
                track.EnterX = 0f;
                track.EnterY = 0f;
            }
            else
            {
                Animate(track, entry, t);
            }
        }

        if (track.Hold > 0f)
        {
            track.Hold -= dt;
        }
        else
        {
            track.Glide *= decay;
        }

        if (Mathf.Abs(track.Glide) < 0.004f)
        {
            track.Glide = 0f;
        }

        Place(track);
        return track.Enter != null || track.Glide != 0f;
    }

    private static void Release(Track track)
    {
        if (track.Enter != null)
        {
            Finish(track.Enter);
        }

        track.Enter = null;
        track.EnterX = 0f;
        track.EnterY = 0f;
        track.Glide = 0f;
        if (track.T != null)
        {
            Place(track);
        }
    }

    private static void Place(Track track)
    {
        
        Vector3 current = track.T.localPosition;
        track.T.localPosition = new Vector3(track.X + track.EnterX, track.Y + track.EnterY + track.Glide, current.z);
    }

    private static void Move(Transform transform, Vector3 home, float dx)
    {
        
        Vector3 current = transform.localPosition;
        transform.localPosition = new Vector3(home.x + dx, current.y, current.z);
    }

    private static void Finish(Entry entry)
    {
        if (entry.Root != null)
        {
            entry.Root.localScale = entry.RootScale;
        }

        if (entry.Avatar != null)
        {
            entry.Avatar.localScale = entry.AvatarScale;
        }

        if (entry.Bg != null)
        {
            
            Color bg = entry.BgColor;
            bg.a = entry.Bg.color.a;
            entry.Bg.color = bg;
        }

        Color name = ReadColor(entry.NameText);
        name.a = entry.NameColor.a;
        WriteColor(entry.NameText, name);

        Color msg = ReadColor(entry.MsgText);
        msg.a = entry.MsgColor.a;
        WriteColor(entry.MsgText, msg);

        if (entry.NameTr != null)
        {
            entry.NameTr.localPosition = entry.NamePos;
        }

        if (entry.MsgTr != null)
        {
            entry.MsgTr.localPosition = entry.MsgPos;
        }
    }

    private static bool Fade(TMP_Text text, float baseAlpha, ref float last, float alpha, float steps)
    {
        float q = alpha >= 1f ? 1f : Mathf.Floor(alpha * steps) / steps;
        if (q == last)
        {
            return q >= 1f;
        }

        last = q;
        Color c = ReadColor(text);
        c.a = baseAlpha * q;
        WriteColor(text, c);
        return q >= 1f;
    }

    private static float HoldAlpha(SpriteRenderer bg, Color snapshot)
    {
        return snapshot.a > 0.01f ? snapshot.a : bg.color.a;
    }

    private static void Animate(Track track, Entry entry, float t)
    {
        Look style = entry.Style;
        float u = t * style.Speed * Gait();
        float off = 1f - Back(u / 0.42f, 1.4f);
        track.EnterX = entry.Side * style.Slide * off;
        track.EnterY = -style.Rise * off;

        if (entry.Root != null)
        {
            float scale = 1f - style.Dip * off;
            entry.Root.localScale = new Vector3(entry.RootScale.x * scale, entry.RootScale.y * scale, entry.RootScale.z);
        }

        if (entry.Avatar != null)
        {
            float pop = style.Pop + (1f - style.Pop) * Back((u - 0.04f) / 0.42f, 2.4f);
            entry.Avatar.localScale = new Vector3(entry.AvatarScale.x * pop, entry.AvatarScale.y * pop, entry.AvatarScale.z);
        }

        if (entry.NameTr != null)
        {
            Move(entry.NameTr, entry.NamePos, entry.Side * style.Slide * 0.35f * (1f - Back((u - 0.03f) / 0.45f, 1.2f)));
        }

        if (entry.MsgTr != null)
        {
            Move(entry.MsgTr, entry.MsgPos, entry.Side * style.Slide * 0.55f * (1f - Back((u - 0.08f) / 0.5f, 1.2f)));
        }

        if (entry.Bg != null && !entry.BgDone)
        {
            float fade = Smooth(u / 0.2f);
            float flash = style.Flash * (1f - Smooth(u / 0.55f));
            Color color = flash > 0.001f ? Color.Lerp(entry.BgColor, entry.FlashColor, 0.4f * flash) : entry.BgColor;
            
            color.a = HoldAlpha(entry.Bg, entry.BgColor);
            
            if (entry.BgColor.r > 0.9f && entry.BgColor.g > 0.9f && entry.BgColor.b > 0.9f && AmethystConfig.DarkChatTheme.Value)
            {
                Color current = entry.Bg.color;
                color.r = current.r;
                color.g = current.g;
                color.b = current.b;
            }

            entry.Bg.color = color;
            entry.BgDone = fade >= 1f && flash <= 0.001f;
        }

        float steps = style.Steps;
        if (!entry.NameDone)
        {
            entry.NameDone = Fade(entry.NameText, entry.NameColor.a, ref entry.NameLast, Smooth((u - 0.04f) / 0.22f), steps);
        }

        if (!entry.MsgDone)
        {
            entry.MsgDone = Fade(entry.MsgText, entry.MsgColor.a, ref entry.MsgLast, Smooth((u - 0.1f) / 0.22f), steps);
        }
    }
}



