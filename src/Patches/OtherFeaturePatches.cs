using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using AmongUs.Data;
using AmongUs.GameOptions;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace Amethyst.Patches;

[HarmonyPatch(typeof(ChatController), "Awake")]
internal static class AmethystChatHistoryPatch
{
    public static void Postfix(ChatController __instance)
    {
        if (!AmethystConfig.ChatHistory80.Value || __instance == null || __instance.chatBubblePool == null) return;
        try
        {
            __instance.chatBubblePool.poolSize = 80;
            __instance.chatBubblePool.ReclaimOldest();
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ChatController), "Update")]
internal static class AmethystChatHistoryUpdatePatch
{
    public static void Postfix(ChatController __instance)
    {
        // 绗?199 杞細骞跺叆 ChatAux 璁￠噺锛堜笌鍏朵綑涓変釜 ChatController.Update 琛ヤ竵鍚屼竴绫伙級銆?
        int p = AmethystPerfProbe.Begin(Section.S_ChatAux);
        try
        {
            if (!AmethystConfig.ChatHistory80.Value || __instance == null || __instance.chatBubblePool == null) return;
            if (__instance.chatBubblePool.poolSize != 80)
                __instance.chatBubblePool.poolSize = 80;
        }
        catch { }
        finally { AmethystPerfProbe.End(p, Section.S_ChatAux); }
    }
}

[HarmonyPatch]
internal static class AmethystForceShowStartPatches
{
    private static PassiveButton _cancelButton;
    private static GameStartManager _owner;

    // 涓庡弬鑰冩ā缁勪竴鑷达細鎸夐挳缂哄け鎴栨墍灞?GameStartManager 瀹炰緥鏇存崲鏃堕噸寤恒€?
    // 涓嶄娇鐢ㄦ寔涔呭寲 _initialized 鏍囧織鈥斺€旈偅浼氬鑷存崲鎴垮悗鎸夐挳姘镐笉閲嶅缓锛堝亣null寮曠敤锛夈€?
    private static void EnsureCancelButton(GameStartManager gs)
    {
        if (_cancelButton != null && _owner == gs) return;
        if (gs == null || gs.StartButton == null) return;
        try
        {
            _cancelButton = UnityEngine.Object.Instantiate(gs.StartButton, gs.transform);
            _cancelButton.name = "AmethystForceShowStartCancel";
            _cancelButton.gameObject.SetActive(false);

            TextMeshPro label = _cancelButton.GetComponentInChildren<TextMeshPro>();
            if (label != null)
            {
                DestroyTranslators(label.gameObject);
                label.text = string.Empty;
            }

            SpriteRenderer inactive = _cancelButton.inactiveSprites.GetComponent<SpriteRenderer>();
            SpriteRenderer active = _cancelButton.activeSprites.GetComponent<SpriteRenderer>();
            if (inactive != null) inactive.color = new Color(0.8f, 0f, 0f, 1f);
            if (active != null) active.color = Color.red;
            Transform shine = _cancelButton.inactiveSprites.transform.Find("Shine");
            if (shine != null) shine.gameObject.SetActive(false);

            _cancelButton.activeTextColor = _cancelButton.inactiveTextColor = Color.white;
            _cancelButton.OnClick = new Button.ButtonClickedEvent();
            _cancelButton.OnClick.AddListener((Action)gs.ResetStartState);
            _owner = gs;
        }
        catch { }
    }

    // 鍙傝€冩ā缁?Extensions.DestroyTranslator 鐨勭Щ妞?
    private static void DestroyTranslators(GameObject go)
    {
        if (go == null) return;
        foreach (Transform child in go.transform)
            DestroyTranslators(child.gameObject);
        var comps = go.GetComponents<MonoBehaviour>();
        foreach (var c in comps)
        {
            if (c != null && c.GetType().Name.Contains("Translator"))
                UnityEngine.Object.Destroy(c);
        }
    }

    [HarmonyPatch(typeof(GameStartManager), "Start")]
    [HarmonyPostfix]
    private static void StartPostfix(GameStartManager __instance)
    {
        // 涓庡弬鑰冩ā缁勪竴鑷达細鏃犳潯浠跺垱寤哄彇娑堟寜閽紱閰嶇疆寮€鍏冲彧鍦?Update 涓帶鍒惰涓恒€?
        // 杩欐牱鍦ㄥぇ鍘呬腑閫斿紑鍚姛鑳戒篃绔嬪嵆鐢熸晥锛屾棤闇€閲嶈繘鎴块棿銆?
        EnsureCancelButton(__instance);
        DumpLobbyStructure(__instance);
        // 立即应用一次，避免进入大厅后 1~2 帧仍是原尺寸
        try { ApplyLobbyPaneScale(__instance); } catch { }
    }

    // ===== 涓€娆℃€ц瘖鏂細鎵撳嵃澶у巺闈㈡澘涓庢寜閽殑鐪熷疄杩愯鏃剁粨鏋勶紙寰瘉锛屼笉鐬庣寽锛?=====
    private static bool _diagDone;
    private static void DumpLobbyStructure(GameStartManager gs)
    {
        if (_diagDone || gs == null) return;
        _diagDone = true;
        try
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[LobbyDiag] GameStartManager structure:");

            LobbyInfoPane lip = gs.LobbyInfoPane;
            sb.AppendLine($"  gs.LobbyInfoPane: {(lip == null ? "NULL" : lip.gameObject.name + " | parent=" + (lip.transform.parent != null ? lip.transform.parent.name : "null") + " | children=" + lip.transform.childCount)}");

            try
            {
                LobbyInfoPane inst = LobbyInfoPane.Instance;
                sb.AppendLine($"  LobbyInfoPane.Instance: {(inst == null ? "NULL" : inst.gameObject.name + " | parent=" + (inst.transform.parent != null ? inst.transform.parent.name : "null") + " | children=" + inst.transform.childCount)}");
            }
            catch (Exception e) { sb.AppendLine($"  LobbyInfoPane.Instance: EXCEPTION {e.GetType().Name}"); }

            var hip = gs.HostInfoPanel;
            sb.AppendLine($"  gs.HostInfoPanel: {(hip == null ? "NULL" : hip.gameObject.name)}");

            if (lip != null)
            {
                var bg = lip.InfoPaneBackground;
                sb.AppendLine($"  InfoPaneBackground: {(bg == null ? "NULL" : "parent=" + (bg.transform.parent != null ? bg.transform.parent.name : "null"))}");
                var copy = lip.CopyCodeText;
                sb.AppendLine($"  CopyCodeText: {(copy == null ? "NULL" : "parent=" + copy.transform.parent.name)}");
            }

            sb.AppendLine($"  gs.EditButton: {(gs.EditButton == null ? "NULL" : "parent=" + gs.EditButton.transform.parent.name)}");
            sb.AppendLine($"  gs.HostViewButton: {(gs.HostViewButton == null ? "NULL" : "parent=" + gs.HostViewButton.transform.parent.name)}");
            sb.AppendLine($"  gs.ClientViewButton: {(gs.ClientViewButton == null ? "NULL" : "parent=" + gs.ClientViewButton.transform.parent.name)}");
            sb.AppendLine($"  gs.GameRoomButton: {(gs.GameRoomButton == null ? "NULL" : "parent=" + gs.GameRoomButton.transform.parent.name)}");
            sb.AppendLine($"  gs.PlayerCounter: {(gs.PlayerCounter == null ? "NULL" : "parent=" + gs.PlayerCounter.transform.parent.name)}");
            sb.AppendLine($"  gs.MapImage: {(gs.MapImage == null ? "NULL" : "parent=" + gs.MapImage.transform.parent.name)}");

            if (gs.StartButton != null)
            {
                var ap = gs.StartButton.GetComponent<AspectPosition>();
                string apDesc = ap == null ? "NONE" : "y=" + ap.DistanceFromEdge.y + " z=" + ap.DistanceFromEdge.z;
                sb.AppendLine($"  StartButton: AP={apDesc} | localPos=" + gs.StartButton.transform.localPosition
                    + " | world=" + gs.StartButton.transform.position
                    + " | parent=" + (gs.StartButton.transform.parent != null ? gs.StartButton.transform.parent.name : "null")
                    + " | parentWorldPos=" + (gs.StartButton.transform.parent != null ? gs.StartButton.transform.parent.position.ToString() : "null")
                    + " | parentScale=" + (gs.StartButton.transform.parent != null ? gs.StartButton.transform.parent.lossyScale.ToString() : "null"));
            }

            // ---- 娣卞害璇婃柇锛氶潰鏉挎牴銆佸€掕鏃躲€佷笂鏂规寜閽?----
            sb.AppendLine("  --- pane root detail ---");
            Transform pane = null;
            try { var i2 = LobbyInfoPane.Instance; if (i2 != null) pane = i2.transform; } catch { }
            if (pane == null && gs.LobbyInfoPane != null) pane = gs.LobbyInfoPane.transform;
            if (pane != null)
            {
                var ap2 = pane.GetComponent<AspectPosition>();
                sb.AppendLine($"  pane: name={pane.name} localPos={pane.localPosition} world={pane.position} localScale={pane.localScale} lossyScale={pane.lossyScale}"
                    + $" AP={(ap2 == null ? "NONE" : "anch=" + ap2.Alignment + " y=" + ap2.DistanceFromEdge.y + " x=" + ap2.DistanceFromEdge.x)}"
                    + $" parent={pane.parent?.name}");
                for (int i = 0; i < pane.childCount; i++)
                {
                    var c = pane.GetChild(i);
                    sb.AppendLine($"    child[{i}] {c.name} localPos={c.localPosition} world={c.position} localScale={c.localScale} children={c.childCount}");
                }
            }

            // 真正承载面板视觉的 AspectSize 层级（上溯整条链，直到 Hud）
            sb.AppendLine("  --- AspectSize chain (visual root) ---");
            Transform vis = gs.LobbyInfoPane != null && gs.LobbyInfoPane.InfoPaneBackground != null
                ? gs.LobbyInfoPane.InfoPaneBackground.transform : null;
            if (vis != null)
            {
                Transform cur = vis;
                int depth = 0;
                while (cur != null && depth < 8)
                {
                    var apx = cur.GetComponent<AspectPosition>();
                    var asz = cur.GetComponent<AspectSize>();
                    sb.AppendLine($"    [{depth}] {cur.name} localPos={cur.localPosition} world={cur.position} localScale={cur.localScale} lossy={cur.lossyScale}"
                        + $" AP={(apx == null ? "-" : apx.Alignment + "@" + apx.DistanceFromEdge.ToString())}"
                        + $" AspectSize={(asz == null ? "-" : "present")}"
                        + $" children={cur.childCount}");
                    cur = cur.parent;
                    depth++;
                }
            }

            sb.AppendLine("  --- countdown / top buttons ---");
            var gst = gs.GameStartText;
            if (gst != null)
            {
                var ap3 = gst.GetComponent<AspectPosition>();
                sb.AppendLine($"  GameStartText: localPos={gst.transform.localPosition} world={gst.transform.position} parent={gst.transform.parent?.name}"
                    + $" AP={(ap3 == null ? "NONE" : "anch=" + ap3.Alignment + " y=" + ap3.DistanceFromEdge.y + " x=" + ap3.DistanceFromEdge.x)} text={gst.text}");
            }
            if (gs.GameStartTextParent != null)
            {
                sb.AppendLine($"  GameStartTextParent: localPos={gs.GameStartTextParent.transform.localPosition} world={gs.GameStartTextParent.transform.position}");
            }
            if (gs.EditButton != null)
            {
                var p = gs.EditButton.transform.parent;
                while (p != null)
                {
                    if (p.name.Contains("Host Buttons") || p.name.Contains("AspectSize") || p.name.ToLower().Contains("hud"))
                    {
                        sb.AppendLine($"  EditButton ancestor: {p.name} world={p.position} localPos={p.localPosition}");
                        var apa = p.GetComponent<AspectPosition>();
                        if (apa != null) sb.AppendLine($"    {p.name} AP: anch={apa.Alignment} y={apa.DistanceFromEdge.y} x={apa.DistanceFromEdge.x}");
                    }
                    p = p.parent;
                }
            }

            AmethystPlugin.Logger.LogInfo(sb.ToString());
        }
        catch { }
    }

    // ===== 开始按钮布局：已并入「整块硬编码定位」=====
    //
    // 原来这里单独把 StartButton 再往下挪 0.06（相对原始坐标）。
    // 现在位置统一由下面的 `ApplyGameStartBlockLayout` 以**绝对值**钉死
    // （GameStartManager 整块 = -2.8），若再叠一次相对偏移，按钮就会在
    // 硬编码位置之上又多飘 0.06，与实际要求不一致 —— 故整体删除。
    //
    // 保留空实现，调用点不动，避免别处引用编译不过。
    private static void ApplyStartButtonLayout(PassiveButton btn, GameStartManager gs)
    {
        // 空实现：位置由 ApplyGameStartBlockLayout 统一硬编码。
    }


    // ===== 大厅下半区（房主信息 + 开始按钮）整块下移 =====
    //
    // 需求来源：截图里「房主：xxx（你）」那行字正被大厅小人的帽子压住。
    //
    // 参考模组 BetterAmongUs（参考模组/BetterAmongUs-1.3.4/src/Patches/Gameplay/LobbyPatch.cs:34-46）：
    //   在 LobbyBehaviour.Update 里每帧把 `HudManager.transform.Find("GameStartManager")`
    //   设为 localY = -2.8 —— 也就是把「房主信息 + 开始按钮」当作**一整块**往下放，
    //   给大厅中央站着的小人让位。它另外还把 StartButton 的父级挂进 HostInfoPanel，
    //   让按钮跟着房主信息走；我们已有独立的按钮布局，不重复绑父级，只做整块下移。
    //
    // 我们此前只单独挪 StartButton（±0.06），房主信息那一块从没被移动过 —— 所以被人物压住。
    //
    // ===== 硬编码（强制生效，无开关）=====
    //
    // 按需求「更好的开始按钮位置改为硬编码、强制开启」：
    //   直接沿用 BAU 验证过的绝对值 localY = -2.8，不做相对计算、不读任何配置。
    //
    // 取值演进（实测，2026-10-17，1602x693 截图，1 world unit ≈ 115 px）：
    //   ① 照抄 BAU 的 -2.8 —— **过低**：开始按钮下沿直接压住底部状态条
    //      （「延迟 / 帧率 / 服务器 / 房间 / 房主」那一行）。
    //   ② 收到 -2.4 —— **偏高**：按钮和状态条之间空出一大截，不好看。
    //   ③ 收到 -2.55 仍偏上，最终取 -2.61：按钮下沿悬在状态条上方一小段，不重合、也不空。
    //      相对 -2.8 上抬 0.19（≈22 px）；相对 -2.4 下压 0.21（≈24 px）。
    //
    //   原版根节点 localY ≈ -2.36（StartGameArea.parentWorldPos.y = -2.36，
    //   Hud 的 localY = 0、z = -20）—— 那是「房主：xxx（你）」被小人帽子压住的位置，
    //   所以只会往这个值**下方**调，绝不回到 -2.36 以上。
    //
    //   ⚠ 再往下压就会与状态条重合；再往上抬就会空太多。
    private const float GameStartBlockLocalY = -2.61f;
    private static GameStartManager _gsbOwner;
    private static Transform _gsbRoot;
    private static bool _gsbLogged;

    private static void ApplyGameStartBlockLayout(GameStartManager gs)
    {
        if (gs == null) return;
        Transform root = gs.transform;
        if (root == null) return;

        if (_gsbOwner != gs || _gsbRoot != root)
        {
            _gsbOwner = gs;
            _gsbRoot = root;
            _gsbLogged = false;
            AmethystPlugin.Logger.LogInfo(
                $"[LobbyBlock] root={root.name} origLocalY={root.localPosition.y} world={root.position}");
        }

        Vector3 lp = root.localPosition;
        if (Mathf.Abs(lp.y - GameStartBlockLocalY) > 0.001f)
        {
            lp.y = GameStartBlockLocalY;
            root.localPosition = lp;
        }

        if (!_gsbLogged)
        {
            _gsbLogged = true;
            PassiveButton btn = gs.StartButton;
            AmethystPlugin.Logger.LogInfo(
                $"[LobbyBlock] applied localY={GameStartBlockLocalY} btnWorldY={(btn != null ? btn.transform.position.y : float.NaN)}");
        }
    }

    // ===== 房主标签（LocalLabel）下移 —— 已停用 =====
    //
    // ⚠ 走过的弯路（别再重犯）：
    //   这里曾把 `gs.LocalLabel` 当作「房主：xxx（你）」下移 0.62。
    //   但 `gs.LocalLabel` 其实是**房间面板顶部的标题** ——
    //   本地/离线房间时它显示 `StringNames.LocalLobbyLabel`（中文即「本地大厅」），
    //   与面板内紧随其下的「房间设置」是上下相邻的两行。
    //   把它下移 0.62 正好压到「房间设置」上，于是出现两行字重叠。
    //
    //   「房主：xxx（你）」是 `StringNames.HostYouLabel` 的另一个文本对象，
    //   不在 `LocalLabel` 里 —— 想调那个标签请另找对象，别再动 `LocalLabel`。
    //
    // 结论：`LocalLabel` 属于面板内部布局，交给游戏自己排版；
    //   面板只做整体等比缩放（见 `ApplyLobbyPaneScale`），不再单独移动它。
    //
    // 原实现（下移 0.62）已整体删除，调用点保留为空调用，避免再被误加回来。
    private static void ApplyLocalLabelLayout(GameStartManager gs)
    {
        // 空实现：LocalLabel 保持游戏原生位置。
    }

    // ===== 右侧面板缩放（可开关）=====
    //
    // 实测结构（[LobbyDiag]）：
    //   LobbyInfoPane  (AspectPosition=RightTop x=0.11 y=0.86, parent=Hud)
    //     +- AspectSize   (面板全部视觉元素，15 个子物体)
    //     +- newtabs
    //
    // ⚠ 走过的弯路（别再重犯）：
    //   ① 改 AspectSize.PercentWidth  -> 它按比例重算尺寸，面板反而被撑大；
    //   ② 只缩 AspectSize 的 localScale -> 它内部的文字各自挂了 AspectPosition，
    //      每帧按"屏幕绝对坐标"重新摆位，于是文字没跟着缩、压到下划线上
    //      （"本地大厅" 与下划线重合就是这么来的）。
    //
    // 正确做法：缩面板根 LobbyInfoPane 的 localScale（整体等比缩放，内部相对布局不变），
    // 同时临时停用面板内部那些会抢位的 AspectPosition / AspectSize，
    // 否则它们会把子元素重新拉回原坐标。
    private const float PaneScale = 0.7f;
    private const float PaneShiftLeftX = 0.02f;   // RightTop 锚点：x 增大 = 左移

    private static LobbyInfoPane _paneLip;
    private static Vector3 _paneOrigScale = Vector3.one;
    private static float _paneOrigEdgeX = float.NaN;
    private static bool _paneApplied;
    private static readonly List<Behaviour> _paneDisabled = new List<Behaviour>();

    private static void ApplyLobbyPaneScale(GameStartManager gs)
    {
        LobbyInfoPane lip = null;
        try { if (gs != null) lip = gs.LobbyInfoPane; } catch { }
        if (lip == null) { try { lip = LobbyInfoPane.Instance; } catch { } }
        ApplyLobbyPaneScale(lip);
    }

    internal static void ApplyLobbyPaneScale(LobbyInfoPane lip)
    {
        bool on = AmethystConfig.ScaleLobbyPane != null && AmethystConfig.ScaleLobbyPane.Value;

        if (lip == null) return;

        // 实例变了：恢复旧面板，重新初始化
        if (_paneLip != null && _paneLip != lip)
        {
            RestoreLobbyPane();
        }

        if (_paneLip != lip)
        {
            _paneLip = lip;
            _paneApplied = false;
            _paneOrigScale = lip.transform.localScale;
            var ap0 = lip.GetComponent<AspectPosition>();
            if (ap0 != null) _paneOrigEdgeX = ap0.DistanceFromEdge.x;

            AmethystPlugin.Logger.LogInfo($"[LobbyPane] root={lip.gameObject.name} origScale={_paneOrigScale} origEdgeX={_paneOrigEdgeX}");
        }

        if (on)
        {
            // 首次应用：停用面板内部（子节点）抢位的定位组件（只做一次）。
            // 注意：不要禁用 root LobbyInfoPane 自己的 AspectPosition，
            // 否则它无法在分辨率 / SafeArea 变化时重新锚定面板位置。
            if (!_paneApplied)
            {
                _paneApplied = true;
                _paneDisabled.Clear();
                foreach (var ap in lip.GetComponentsInChildren<AspectPosition>(true))
                {
                    if (ap == null || !ap.enabled) continue;
                    if (ap.gameObject == lip.gameObject) continue; // 保留根组件
                    ap.enabled = false;
                    _paneDisabled.Add(ap);
                }
                foreach (var asz in lip.GetComponentsInChildren<AspectSize>(true))
                {
                    if (asz == null || !asz.enabled) continue;
                    if (asz.gameObject == lip.gameObject) continue;
                    asz.enabled = false;
                    _paneDisabled.Add(asz);
                }
            }

            var wantScale = _paneOrigScale * PaneScale;
            if ((lip.transform.localScale - wantScale).sqrMagnitude > 1e-6f)
                lip.transform.localScale = wantScale;

            var paneAp = lip.GetComponent<AspectPosition>();
            if (paneAp != null && !float.IsNaN(_paneOrigEdgeX))
            {
                float wantX = _paneOrigEdgeX + PaneShiftLeftX;
                if (Mathf.Abs(paneAp.DistanceFromEdge.x - wantX) > 0.002f)
                {
                    var v = paneAp.DistanceFromEdge;
                    v.x = wantX;
                    paneAp.DistanceFromEdge = v;
                    paneAp.AdjustPosition();
                }
            }
        }
        else if (_paneApplied)
        {
            RestoreLobbyPane();
        }
    }

    private static void RestoreLobbyPane()
    {
        if (_paneLip == null) return;
        try
        {
            _paneLip.transform.localScale = _paneOrigScale;

            var ap = _paneLip.GetComponent<AspectPosition>();
            if (ap != null && !float.IsNaN(_paneOrigEdgeX))
            {
                var v = ap.DistanceFromEdge;
                v.x = _paneOrigEdgeX;
                ap.DistanceFromEdge = v;
                ap.AdjustPosition();
            }

            for (int i = 0; i < _paneDisabled.Count; i++)
                if (_paneDisabled[i] != null) _paneDisabled[i].enabled = true;
            _paneDisabled.Clear();
            _paneApplied = false;
        }
        catch { }
    }

    // 倒计时与开始按钮"合体"（参考 BetterAmongUs LobbyPatch.cs:126-138）：
    // 原版倒计时期间会隐藏开始按钮，另用一个独立的 GameStartText 文字显示，
    // 于是出现"一个在上、一个在下"两处元素。
    // 这里改为：隐藏独立的 GameStartTextParent，把倒计时数字写进按钮自身的文字，
    // 按钮位置不动，二者自然重合在同一处。

    // ===== 完全对齐 BetterAmongUs LobbyPatch.cs =====
    //
    // BAU 的写法（原文）：
    //   [HarmonyPatch(typeof(GameStartManager), "Update")] Prefix:
    //       __instance.MinPlayers = 1;
    //   Postfix:
    //       __instance.GameStartTextParent.SetActive(false);
    //       __instance.StartButton.gameObject.SetActive(true);
    //       if (startState == Countdown)
    //           __instance.StartButton.buttonText.text = "取消: N";
    //       else
    //           __instance.StartButton.buttonText.text = "开始";
    //   [HarmonyPatch(typeof(GameStartManager), "BeginGame")] Prefix:
    //       if (startState == Countdown) { StopSound; ResetStartState(); return false; }
    //
    // 倒计时与按钮"一上一下"的根因：原版把倒计时放在独立的 GameStartTextParent，
    // 与开始按钮是两个不同对象。BAU 的做法是**彻底隐藏那个独立对象**，
    // 把倒计时数字写进开始按钮自己的 buttonText —— 于是只有一处元素，自然重合。

    // 与 BAU 一致：直接用游戏内置翻译（StringNames），不依赖本模组语言包
    private static string GetGameString(StringNames name)
    {
        try
        {
            if (TranslationController.Instance != null)
                return TranslationController.Instance.GetString(name);
        }
        catch { }
        return string.Empty;
    }

    [HarmonyPatch(typeof(GameStartManager), "Update")]
    [HarmonyPrefix]
    private static void StartManagerUpdatePrefix(GameStartManager __instance)
    {
        if (__instance == null) return;
        try { __instance.MinPlayers = 1; } catch { }
    }

    [HarmonyPatch(typeof(GameStartManager), "Update")]
    [HarmonyPostfix]
    private static void StartManagerUpdatePostfix(GameStartManager __instance)
    {
        if (__instance == null) return;
        try
        {
            // 非房主：保留游戏原生的倒计时文字（GameStartTextParent），只隐藏开始按钮；
            // 房主：隐藏独立文字，把倒计时写进开始按钮，避免两处重复。

            // 立即应用面板缩放（即使非房主也需要），放在主机判断之前
            try { ApplyLobbyPaneScale(__instance); } catch { }

            // 大厅下半区（房主信息 + 开始按钮）硬编码定位：强制生效、无开关。
            // 同样放在主机判断之前 —— 非房主也要摆正，否则「房主：xxx」那行照样被小人压住。
            try { ApplyGameStartBlockLayout(__instance); } catch { }

            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            {
                // 客户端：倒计时由游戏原生的 GameStartTextParent 显示，不能隐藏；
                // 开始按钮只属于房主。
                __instance.StartButton.gameObject.SetActive(false);
                return;
            }

            // 房主：把倒计时写进开始按钮，隐藏独立的倒计时文字，避免出现两处。
            __instance.GameStartTextParent.SetActive(false);
            __instance.StartButton.gameObject.SetActive(true);

            if (__instance.startState == GameStartManager.StartingStates.Countdown)
            {
                __instance.StartButton.buttonText.text =
                    string.Format("{0}: {1}", GetGameString(StringNames.Cancel), (int)__instance.countDownTimer + 1);
            }
            else
            {
                __instance.StartButton.buttonText.text = GetGameString(StringNames.StartLabel);
            }

            // 位置（与 BAU 不同：按我们的布局）
            ApplyStartButtonLayout(__instance.StartButton, __instance);
            ApplyLocalLabelLayout(__instance);
        }
        catch { }
    }

    [HarmonyPatch(typeof(GameStartManager), "BeginGame")]
    [HarmonyPrefix]
    private static bool StartManagerBeginGamePrefix(GameStartManager __instance)
    {
        if (__instance == null) return true;
        // 只有房主的“再次点击 = 取消倒计时”需要拦截；客户端不应重置开始状态。
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return true;
        if (__instance.startState != GameStartManager.StartingStates.Countdown) return true;
        try
        {
            if (SoundManager.Instance != null && __instance.gameStartSound != null)
                SoundManager.Instance.StopSound(__instance.gameStartSound);
            __instance.ResetStartState();
        }
        catch { }
        return false;
    }



    [HarmonyPatch(typeof(GameStartManager), "ResetStartState")]
    [HarmonyPrefix]
    private static void ResetPrefix(GameStartManager __instance)
    {
        if (_cancelButton == null) return;
        try
        {
            _cancelButton.gameObject.SetActive(false);
            if (__instance.StartButton != null) __instance.StartButton.gameObject.SetActive(true);
            if (__instance.startState == GameStartManager.StartingStates.Countdown && SoundManager.Instance != null)
                SoundManager.Instance.StopSound(__instance.gameStartSound);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(KillOverlay), "ShowKillAnimation", new[] { typeof(NetworkedPlayerInfo), typeof(NetworkedPlayerInfo) })]
internal static class AmethystSkipKillAnimationInfoPatch
{
    public static bool Prefix() => !AmethystConfig.SkipKillAnimation.Value;
}

[HarmonyPatch(typeof(KillOverlay), "ShowKillAnimation", new[] { typeof(OverlayKillAnimation), typeof(NetworkedPlayerInfo), typeof(NetworkedPlayerInfo) })]
internal static class AmethystSkipKillAnimationOverlayPatch
{
    public static bool Prefix() => !AmethystConfig.SkipKillAnimation.Value;
}

[HarmonyPatch(typeof(KillOverlay), "ShowKillAnimation", new[] { typeof(OverlayKillAnimation), typeof(KillOverlayInitData) })]
internal static class AmethystSkipKillAnimationInitPatch
{
    public static bool Prefix() => !AmethystConfig.SkipKillAnimation.Value;
}

internal static class AmethystSabotageCooldownState
{
    internal static readonly Dictionary<SystemTypes, TextMeshPro> TimerTexts = new Dictionary<SystemTypes, TextMeshPro>();

    // 鈿?鎬ц兘锛堢 162 杞級锛氳繖娈佃鎸傚湪 `InfectedOverlay.FixedUpdate`锛堝浐瀹?50Hz锛変笂锛?
    //   鍘熸潵姣忔姹傚€奸兘瑕佸仛**鍙嶅皠**锛?
    //       sys.GetType().GetProperty("IsActive")     鈫?鎸夊悕瀛楁煡灞炴€?
    //       prop.GetValue(sys)
    //   鑰屽灞傛瘡涓埧闂磋繕浼氬鏁翠釜 TimerTexts 鍐嶅仛涓€娆?`Keys.Any(...)`锛堝唴灞傚張閫愪釜鍙嶅皠锛夛紝
    //   鍗曟 FixedUpdate 灏辨槸涓ょ櫨澶氭"鎸夊悕瀛楁煡灞炴€? 鈥斺€?IL2CPP 涓嬫槸姣绾у紑閿€锛岀洿鎺ラ《鐖嗗抚棰勭畻銆?
    //   鐜板湪锛氣憼 鎸夌被鍨嬬紦瀛?PropertyInfo锛?鎸夊悕瀛楁煡"鍙仛涓€娆★紝涔嬪悗鍙墿 GetValue锛夛紱
    //         鈶?鍚屼竴甯у唴鍚屼竴涓?system 鍙眰鍊间竴娆★紙memo锛夛紝鎶婁笂闈㈢殑 O(n虏) 鎽婂钩鎴?O(n)銆?
    private static readonly Dictionary<Type, PropertyInfo> IsActiveProps = new Dictionary<Type, PropertyInfo>();
    private static readonly Dictionary<SystemTypes, bool> ActiveThisFrame = new Dictionary<SystemTypes, bool>();
    private static int _activeFrame = -1;

    // 鎸夌被鍨嬭В鏋愬苟缂撳瓨 `IsActive` 灞炴€с€傜紦瀛?null 涔熸槸鏈夋晥缁撴灉锛堣〃绀鸿绫诲瀷娌℃湁杩欎釜灞炴€э級銆?
    private static PropertyInfo ResolveIsActive(Type type)
    {
        if (type == null) return null;
        if (IsActiveProps.TryGetValue(type, out PropertyInfo cached)) return cached;

        PropertyInfo prop = null;
        try { prop = type.GetProperty("IsActive"); } catch { prop = null; }
        IsActiveProps[type] = prop;
        return prop;
    }

    internal static bool IsActive(SystemTypes type)
    {
        try
        {
            // 鍚屼竴甯у唴锛堝彲鑳借窇浜嗗娆?FixedUpdate锛夊鍚屼竴涓?system 鍙弽灏勪竴娆°€?
            // 鐮村潖鐘舵€佺敱缃戠粶 RPC 鍦ㄤ富绾跨▼鏇存柊锛屼竴甯т箣鍐呬笉浼氬彉锛屾墍浠ヨ繖涓?memo 鏄畨鍏ㄧ殑銆?
            int frame = Time.frameCount;
            if (frame != _activeFrame)
            {
                _activeFrame = frame;
                ActiveThisFrame.Clear();
            }
            else if (ActiveThisFrame.TryGetValue(type, out bool memo))
            {
                return memo;
            }

            bool result = false;
            if (ShipStatus.Instance != null
                && ShipStatus.Instance.Systems.TryGetValue(type, out ISystemType sys)
                && sys != null)
            {
                PropertyInfo prop = ResolveIsActive(sys.GetType());
                if (prop != null) result = prop.GetValue(sys) is bool value && value;
            }

            ActiveThisFrame[type] = result;
            return result;
        }
        catch { return false; }
    }
}

[HarmonyPatch(typeof(InfectedOverlay), "FixedUpdate")]
internal static class AmethystSabotageCooldownPatch
{
    public static void Postfix(InfectedOverlay __instance)
    {
        if (AmethystCompat.OtherSabotageCd) return;
        if (!AmethystConfig.SabotageCooldownDisplay.Value || __instance == null || __instance.sabSystem == null) return;
        try
        {
            float exact = __instance.sabSystem.PercentCool * (__instance.sabSystem.initialCooldown ? 10f : 30f);
            int remaining = Mathf.CeilToInt(exact);
            for (int i = 0; i < __instance.rooms.Count; i++)
            {
                MapRoom room = __instance.rooms[i];
                if (!room.special || room.special.transform == null) continue;
                if (!AmethystSabotageCooldownState.TimerTexts.TryGetValue(room.room, out TextMeshPro text) || text == null || text.transform.parent != room.special.transform)
                {
                    if (text != null) UnityEngine.Object.Destroy(text.gameObject);
                    if (HudManager.Instance == null || HudManager.Instance.KillButton == null || HudManager.Instance.KillButton.cooldownTimerText == null) continue;
                    text = UnityEngine.Object.Instantiate(HudManager.Instance.KillButton.cooldownTimerText, room.special.transform, true);
                    text.alignment = TextAlignmentOptions.Center;
                    text.transform.localPosition = new Vector3(0f, -0.4f, 0f);
                    text.overflowMode = TextOverflowModes.Overflow;
                    text.enableWordWrapping = false;
                    text.color = Color.white;
                    text.fontSize = text.fontSizeMax = text.fontSizeMin = 2.5f;
                    text.sortingOrder = 100;
                    text.gameObject.SetActive(true);
                    AmethystSabotageCooldownState.TimerTexts[room.room] = text;
                }
                bool active = AmethystSabotageCooldownState.IsActive(room.room);

                // 鈿?鎬ц兘锛堢 162 杞級锛氫笉鐢?LINQ 鈥斺€?`Any(lambda)` 姣忔閮借鍒嗛厤闂寘 + 鏋氫妇鍣紝
                //   鑰岃繖娈佃窇鍦?FixedUpdate 鐨勫唴灞傛埧闂村惊鐜噷銆傝繖閲岀瓑浠锋敼鍐欎负鏄惧紡閬嶅巻
                //   锛堥浂鍒嗛厤銆佸彲鎻愬墠 break锛夈€傚彧璇?TimerTexts锛岄亶鍘嗘湡闂翠笉浼氭敼鍔ㄥ畠锛?
                //   鎵€浠ヤ笉瀛樺湪"杈归亶鍘嗚竟淇敼"鐨?InvalidOperationException 椋庨櫓銆?
                bool otherActive = false;
                foreach (KeyValuePair<SystemTypes, TextMeshPro> timerEntry in AmethystSabotageCooldownState.TimerTexts)
                {
                    if (timerEntry.Key == room.room) continue;
                    if (AmethystSabotageCooldownState.IsActive(timerEntry.Key)) { otherActive = true; break; }
                }
                if (active || otherActive || __instance.DoorsPreventingSabotage)
                {
                    text.text = active && !__instance.DoorsPreventingSabotage ? "<b><#ff0000>鈻?/color></b>" : "<b><#ff0000>鈯?/color></b>";
                    text.enabled = true;
                }
                else if (remaining <= 0) text.enabled = false;
                else
                {
                    text.text = exact < 10f ? $"<b><#ffff00>{exact:F1}</color></b>" : $"<b><#ffff00>{remaining}</color></b>";
                    text.enabled = true;
                }
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(ActionButton), "SetCoolDown")]
internal static class AmethystCooldownTextPatch
{
    public static void Postfix(ActionButton __instance, float timer)
    {
        if (AmethystCompat.OtherCooldown) return;
        if (!(AmethystConfig.BetterCooldownDisplay.Value || AmethystConfig.CooldownDecimals.Value) || __instance == null || __instance.cooldownTimerText == null) return;
        Format(__instance, timer);
    }

    // 鈿?鎬ц兘锛堢 162 杞級锛氬喎鍗?鎸佺画鏃堕棿鏄?*姣忓抚**琚┍鍔ㄧ殑锛圫etCoolDown / SetFillUp锛夛紝
    //   鑰屾樉绀哄唴瀹瑰彧鐢?閲忓寲鍚庣殑妗ｄ綅"鍐冲畾锛?10s 涓€浣嶅皬鏁般€?=10s 鍚戜笂鍙栨暣鏁存暟锛夛紝
    //   鍚屼竴妗ｄ綅姘歌繙鏄悓涓€涓叉枃瀛?鈥斺€?浜庢槸鎸夋。浣嶇紦瀛橈紝鎶婂垎閰嶉鐜囦粠"姣忓抚姣忔寜閽竴娆?ToString"
    //   闄嶅埌"姣忓彉涓€妗ｄ竴娆?锛?10s 鏃剁害姣?0.1 绉掍竴娆★級銆?
    //   鍐欏洖鍓嶈繕浼氬拰鎸夐挳涓?*褰撳墠**鐨勬枃鏈瘮瀵癸細>=10s 鏃舵垜浠畻鍑虹殑鍜屽師鐗堝畬鍏ㄤ竴鑷达紝
    //   杩欎竴姣斿氨鐩存帴璺宠繃锛岃繛 TMP 缃戞牸閮戒笉浼氳寮勮剰銆?
    private static readonly Dictionary<int, string> TextCache = new Dictionary<int, string>(160);

    // 妗ｄ綅缂栫爜锛? = 绌猴紱1000 + tenths(1..99) = 10 绉掑唴涓€浣嶅皬鏁帮紱2000 + ceil = 10 绉掍互涓婃暣鏁般€?
    private static string TextOf(int key)
    {
        if (key <= 0) return string.Empty;
        if (TextCache.TryGetValue(key, out string hit) && hit != null) return hit;

        string made = key >= 2000 ? (key - 2000).ToString() : ((key - 1000) / 10f).ToString("0.0");
        TextCache[key] = made;
        return made;
    }

    // 鎶?(10s 鍐呬竴浣嶅皬鏁般€?0s 浠ヤ笂鍚戜笂鍙栨暣鏁存暟) 鐨勬牸寮忓寲鎶藉嚭鏉ュ叡鐢?鈥斺€?
    // 鍐峰嵈锛圫etCoolDown锛変笌鎶€鑳芥寔缁椂闂达紙SetFillUp锛夐兘鐢ㄥ畠銆?
    internal static void Format(ActionButton button, float timer)
    {
        try
        {
            if (button == null || button.cooldownTimerText == null) return;

            // 鏇村ソ鐨凜D锛氬墿浣?< 0.1s 绔嬪嵆娓呯┖锛堜笉鏄剧ず 0.0锛夛紱>= 10s 鐢ㄥ悜涓婂彇鏁存暣鏁帮紙11銆?2鈥︼級锛?
            // < 10s 鐢ㄤ竴浣嶅皬鏁帮紝鏈€澶у彧鏄剧ず 9.9鈥斺€旂粷涓嶆樉绀?鈥?0鈥?鈥?0.0鈥濓紙10.0 閭ｅ抚鐩存帴鏄剧ず 9.9锛夈€?
            // 鍏堥噺鍖栨垚"妗ｄ綅"锛坘ey锛夊啀鏌ョ紦瀛橈紱涓ゅ Clamp 鍚屾椂璧峰埌闃叉暣鏁版孩鍑虹殑浣滅敤
            // 锛坈eil 涓€鏃︽孩鍑轰細鍙樻垚璐熸暟锛岀洿鎺ョ牬鍧忎笅闈㈢殑缂栫爜锛屾墍浠ュ繀椤诲す浣忥級銆?
            int key;
            if (timer < 0.1f) key = 0;
            else if (timer > 10f) key = 2000 + Mathf.Clamp(Mathf.CeilToInt(timer), 11, 9999);
            else key = 1000 + Mathf.Clamp(Mathf.FloorToInt(timer * 10f), 1, 99);

            string want = TextOf(key);
            if (button.cooldownTimerText.text != want) button.cooldownTimerText.text = want;
        }
        catch { }
    }
}

// 鎶€鑳?*鎸佺画鏃堕棿**鐨勬樉绀恒€?
//
// `ActionButton` 涓婃湁涓や釜绛惧悕瀹屽叏鐩稿悓鐨勬柟娉曪紝鐢ㄩ€斿嵈涓嶅悓锛?
//     SetCoolDown(float timer, float maxTimer)  鈫?鍐峰嵈鍊掕鏃?
//     SetFillUp  (float timer, float maxTimer)  鈫?鎶€鑳界敓鏁堝悗鐨?*鎸佺画鏃堕棿**鏉?
// 涔嬪墠鍙粰 `SetCoolDown` 鍔犱簡灏忔暟锛屾墍浠ユ寔缁椂闂翠竴鐩村彧鏄剧ず鏁存暟锛?
// 杩欓噷琛ヤ笂鍚屾牱鐨勬牸寮忓寲 鈥斺€?銆屽紑浜嗘妧鑳戒箣鍚庣殑閭ｆ鎸佺画鏃堕棿涔熶細鏄剧ず灏忔暟鐐广€嶃€?
[HarmonyPatch(typeof(ActionButton), "SetFillUp")]
internal static class AmethystDurationTextPatch
{
    // 鏈€杩戜竴娆°€屾寔缁椂闂淬€嶉┍鍔ㄧ殑鏃堕棿鎴筹細
    // 渚?AmethystCooldownColorState.Tick 鍒ゆ柇銆岀幇鍦ㄦ寜閽笂鐨勫瓧鏄寔缁椂闂达紝鍒竻鎺夈€嶃€?
    internal static float LastFillAt;

    public static void Postfix(ActionButton __instance, float timer)
    {
        if (AmethystCompat.OtherCooldown) return;
        if (!(AmethystConfig.BetterCooldownDisplay.Value || AmethystConfig.CooldownDecimals.Value) || __instance == null || __instance.cooldownTimerText == null) return;
        try
        {
            LastFillAt = Time.realtimeSinceStartup;
            AmethystCooldownTextPatch.Format(__instance, timer);
        }
        catch { }
    }
}

internal static class AmethystCooldownColorState
{
    private static readonly HashSet<RoleTypes> ImpostorRoles = new HashSet<RoleTypes>
    {
        RoleTypes.Impostor, RoleTypes.ImpostorGhost, RoleTypes.Shapeshifter, RoleTypes.Phantom, RoleTypes.Viper
    };

    internal static void Tick(HudManager __instance)
    {
        if (AmethystCompat.OtherCooldown) return;
        if (!AmethystConfig.BetterCooldownDisplay.Value || __instance == null) return;
        try
        {
            Color color = PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.Data != null && PlayerControl.LocalPlayer.Data.Role != null && ImpostorRoles.Contains(PlayerControl.LocalPlayer.Data.Role.Role)
                ? new Color32(255, 90, 90, 255) : new Color32(140, 255, 255, 255);
            // 鈿?鎬ц兘锛堢 162 杞級锛氬師鏉ヨ繖閲屾槸姣忓抚 `new ActionButton[]{...}` 鈥斺€?姣忓抚涓€娆℃暟缁勫垎閰嶃€?
            //   鏀规垚閫愪釜搴旂敤锛岀儹璺緞涓婇浂鍒嗛厤锛堥『搴忋€佸垽绌鸿涔変笌鍘熸潵鐨?foreach 瀹屽叏涓€鑷达級銆?
            Apply(__instance.AbilityButton, color);
            Apply(__instance.SecondaryAbilityButton, color);
            Apply(__instance.KillButton, color);
            Apply(__instance.ImpostorVentButton, color);
        }
        catch { }
    }

    private static void Apply(ActionButton button, Color color)
    {
        if (button == null || button.cooldownTimerText == null) return;
        button.cooldownTimerText.color = color;
        // 娉ㄦ剰锛氭妧鑳?*鎸佺画鏃堕棿**鏈熼棿 isCoolingDown 鍙兘涓?false锛?
        // 鑻ユ鏃剁洿鎺ユ竻绌哄氨浼氭妸鎸佺画鏃堕棿鐨勬暟瀛楁摝鎺夛紙鑰屼笖瀹冩槸姣忓抚琚┍鍔ㄧ殑锛夈€?
        // 鐢ㄣ€屾渶杩戞湁娌℃湁 SetFillUp銆嶆潵鍒ゆ柇锛岄伩鍏嶈娓呫€?
        if (!button.isCoolingDown && Time.realtimeSinceStartup - AmethystDurationTextPatch.LastFillAt > 0.5f)
            button.cooldownTimerText.text = string.Empty;
    }
}

[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystBetterLoadingPatch
{
    // 瓒呮椂淇濇姢锛氬嵆浣挎湁瀹㈡埛绔崱浣忎篃涓嶄細璁╄繘搴︽潯姘镐箙鍋滅暀銆?
    private const float MaxLoadingSeconds = 30f;

    // 杩涘害鏉″钩婊戦€熺巼锛氭瘡绉掓渶澶у彉鍖栭噺锛堢櫨鍒嗘瘮锛夈€傝杩涘害鏉″寑閫熸帹杩涖€佷笉鐬Щ銆?
    private const float SmoothRate = 75f;

    // 闈炴埧涓伙紙鎴块棿鎴愬憳锛夌"姝ｅ湪鐢熸垚鍦板浘"闃舵**鎷夸笉鍒扮湡瀹炶繘搴?*锛堣 BuildStatus 閲岀殑璇存槑锛夛紝
    // 杩欐鏃堕棿鐢ㄤ竴鏉℃寜鏃堕棿鐖崌鐨勪及绠楁洸绾匡細30% 鈫?68%锛屽巻鏃剁害 8.5 绉掋€?
    private const float ClientMapRampSeconds = 8.5f;

    private static bool _running;
    private static bool _allowLoadingBarUpdate;
    private static bool _hidden;
    private static float _loadingStartedAt;
    private static float _shownPercent; // 瀹為檯鏄剧ず鐨勭櫨鍒嗘瘮锛堝钩婊戝悗鐨勫€硷級
    private static float _gamePercent = -1f; // 娓告垙鑷繁涓婃姤杩囩殑杩涘害锛?~100锛夛紝-1 = 杩樻病鏈?
    private static float _mapWaitStart = -1f; // 鎴愬憳绔?鐢熸垚鍦板浘"闃舵鐨勮捣绠楁椂鍒?
    private static GameObject _loadingBar; // 缂撳瓨寮曠敤锛岄槻姝?LoadingBarManager 閿€姣佸悗鏃犳硶闅愯棌
    internal static bool Active => _running;
    internal static bool AllowLoadingBarUpdate => _allowLoadingBarUpdate;

    // 璁板綍"娓告垙鑷繁"涓婃姤鐨勮繘搴︺€?
    // 鎴戜滑鐨?SetLoadingPercent 浼氭妸 _allowLoadingBarUpdate 缃湡锛屾墍浠ヨ兘鍖哄垎鏉ユ簮锛?
    // 涓嶆槸鎴戜滑鍙戠殑锛屽氨鏄父鎴忓彂鐨?鈥斺€?灏戞暟鎯呭喌涓嬫父鎴忓湪闈炴埧涓荤涔熷彲鑳戒笂鎶ワ紝閭ｆ椂鐢ㄥ畠鐨勫€兼洿鐪熷疄銆?
    internal static void NoteGamePercent(float percent)
    {
        try
        {
            if (float.IsNaN(percent) || percent < 0f) return;
            if (percent > 100f) percent = 100f;
            if (percent > _gamePercent) _gamePercent = percent;   // 鍙涓嶅噺
        }
        catch { }
    }

    // 绔嬪嵆闅愯棌鍔犺浇鏉″苟鍋滄鍔犺浇鍗忕▼锛堜緵 SHHH 鎾斁/鎵嬪姩绂诲紑鏃惰皟鐢級
    internal static void HideBar()
    {
        _hidden = true;
        try { if (_loadingBar != null) _loadingBar.SetActive(false); } catch { }
    }

    [HarmonyPostfix]
    private static void Postfix(AmongUsClient __instance)
    {
        if (__instance == null) return;
        // 闃叉涓婃鍗忕▼寮傚父閫€鍑猴紙鍦烘櫙鍒囨崲鏃?finally 鏈墽琛岋級瀵艰嚧鐘舵€佹畫鐣?
        if (_running)
        {
            _running = false;
            _hidden = false;
            _loadingBar = null;
        }
        _gamePercent = -1f;
        _mapWaitStart = -1f;
        try { __instance.StartCoroutine(CoLoading().WrapToIl2Cpp()); }
        catch { }
    }

    // 瀹夊叏缃戯細鎵嬪姩绂诲紑鏃剁‘淇濆姞杞芥潯琚殣钘忥紙鍙傝€?BetterAmongUs ExitGame_Postfix锛?
    [HarmonyPatch(typeof(AmongUsClient), "ExitGame")]
    internal static class AmethystExitGamePatch
    {
        [HarmonyPostfix]
        private static void Postfix() => HideBar();
    }

    private static IEnumerator CoLoading()
    {
        if (_running) yield break;
        _running = true;
        _hidden = false;
        _loadingStartedAt = Time.realtimeSinceStartup;
        _shownPercent = 0f;

        try
        {
            if (LoadingBarManager.Instance == null || LoadingBarManager.Instance.loadingBar == null) yield break;

            // 鎴愬憳鍐嶅紑濮嬫椂锛屾墍鏈夊鎴风鏁版嵁宸茬紦瀛橈紝SHHH 鍙兘鍦ㄦ瀬鐭椂闂村唴鎾斁銆?
            // 鍏堢瓑寰呮暟甯э紝鑻ュ姞杞芥潯琚殣钘忔垨娓告垙宸茬粨鏉?宸茶繘鍏ュ紑鍦哄姩鐢伙紝鍒欒烦杩囨樉绀哄姞杞芥潯銆?
            int quickCheckFrames = 10;
            for (int i = 0; i < quickCheckFrames; i++)
            {
                if (_hidden || IsGameGone() || IntroCutscene.Instance != null) yield break;
                yield return null;
            }

            _loadingBar = LoadingBarManager.Instance.loadingBar.gameObject;
            _loadingBar.SetActive(true);

            bool completed = false;
            while (true)
            {
                if (_hidden || IntroCutscene.Instance != null) break; // SHHH/宸茶繘寮€鍦?鎵嬪姩绂诲紑锛氱珛鍗虫敹璧凤紝闃叉涓庢父鎴忓師鐗?UI 鎶㈠ず鍙ユ焺
                if (AmongUsClient.Instance == null || LoadingBarManager.Instance == null || LoadingBarManager.Instance.loadingBar == null)
                    yield break;

                // 涓€旈€€鍑?缁撴潫锛氱洿鎺ユ敹璧疯繘搴︽潯锛屼笉璋庢姤瀹屾垚銆?
                if (IsGameGone()) yield break;

                if (IsLoadingFinished())
                {
                    completed = true;
                    break;
                }

                // 瓒呮椂锛氬彧鏀惰捣杩涘害鏉★紝鍚屾牱涓嶆樉绀衡€滃姞杞藉畬鎴愨€濄€?
                if (Time.realtimeSinceStartup - _loadingStartedAt >= MaxLoadingSeconds) break;

                BuildStatus(out float target, out string text);
                AdvanceProgress(target);
                SetLoadingPercent(_shownPercent, text);
                yield return null;
            }

            if (completed && !_hidden && IntroCutscene.Instance == null)
            {
                // 骞虫粦鍐插埌 100%锛岄伩鍏嶆渶鍚庝竴璺?
                string done = AmethystText.T("LoadingComplete");
                float t0 = Time.realtimeSinceStartup;
                while (_shownPercent < 100f && Time.realtimeSinceStartup - t0 < 0.5f)
                {
                    if (_hidden || IntroCutscene.Instance != null) break;
                    AdvanceProgress(100f, 180f); // 鏀跺熬绋嶅揩锛屼絾渚濇棫骞虫粦
                    SetLoadingPercent(_shownPercent, done);
                    yield return null;
                }
                if (!_hidden && IntroCutscene.Instance == null)
                {
                    _shownPercent = 100f;
                    SetLoadingPercent(100f, done);
                    yield return new WaitForSeconds(0.2f);
                }
            }
        }
        finally
        {
            // 浣跨敤缂撳瓨鐨勫紩鐢ㄩ殣钘忥紝闃叉 LoadingBarManager 閿€姣佸悗娈嬬暀
            try { if (_loadingBar != null) _loadingBar.SetActive(false); } catch { }
            _running = false;
            _hidden = false;
            _loadingBar = null;
            _gamePercent = -1f;
            _mapWaitStart = -1f;
        }
    }

    // 骞虫粦鎺ㄨ繘锛氬彧澧炰笉鍑忥紝鎸夊浐瀹氶€熺巼閫艰繎鐩爣锛岄伩鍏嶇櫨鍒嗘瘮鐬Щ銆?
    private static void AdvanceProgress(float target, float rate = SmoothRate)
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f || dt > 0.25f) dt = 0.016f;
        float next = Mathf.MoveTowards(_shownPercent, target, rate * dt);
        if (next < _shownPercent) next = _shownPercent; // 涓嶅洖閫€
        _shownPercent = next;
    }

    private static void BuildStatus(out float percent, out string text)
    {
        percent = 10f;
        text = AmethystText.T("StartingGame");

        AmongUsClient client = AmongUsClient.Instance;
        if (client == null) return;

        if (client.GameState != InnerNetClient.GameStates.Started) return;

        if (LobbyBehaviour.Instance != null)
        {
            text = AmethystText.T("EnteringLobby");
            percent = 22f;
            return;
        }

        // 鍦板浘鍔犺浇锛氭埧涓荤浣跨敤鐪熷疄寮傛鍔犺浇杩涘害锛?..1锛夋槧灏勫埌 30% - 70%锛屽苟鏄剧ず鍏蜂綋鐧惧垎姣斻€?
        //
        // 鈿?**闈炴埧涓伙紙鎴块棿鎴愬憳锛夌娌℃湁浠讳綍鐪熷疄杩涘害鏉ユ簮**锛屾墍浠ュ崟鐙蛋涓€鏉′及绠楄矾寰勶細
        //   娓告垙鍙湪鎴夸富绔繚瀛樺湴鍥惧紓姝ュ姞杞藉彞鏌勶紙dnSpy 鏍稿疄锛歚AmongUsClient` 閲屽彧鏈?
        //   `ShipLoadingAsyncHandle` 杩欎竴涓彞鏌勶紝瀹㈡埛绔叏绋嬫嬁涓嶅埌锛夛紝
        //   鑰?`ShipStatus.Instance` 瑕佺瓑鎴夸富鎶婂湴鍥剧敓鎴愬畬銆佸鎴风鍐嶅疄渚嬪寲鍑烘潵鎵嶉潪绌?鈬?
        //   鎴愬憳绔互鍓?*鎭掍负 30%**锛岃〃鐜板氨鏄敤鎴峰弽棣堢殑"涓€鐩村崱鍦?30% 姝ｅ湪鐢熸垚鍦板浘"銆?
        //   锛堝弬鑰冿細BetterAmongUs 鐨勫鎴风鍒嗘敮鐢ㄧ殑鏄悓涓€涓彞鏌勶紝鍙兘鏄剧ず鍥哄畾 40%锛屽悓鏍蜂笉浼氬姩銆傦級
        //
        //   杩欓噷鏀逛负涓ゆ潯鑵匡細鈶?娓告垙鑷繁鑻ヤ笂鎶ヨ繃杩涘害锛屽氨鐢ㄥ畠锛堟洿鐪熷疄锛夛紱
        //   鈶?鍚﹀垯鎸夋椂闂存妸 30% 鈫?68% 骞虫粦鐖崌锛堢害 8.5 绉掞級鍚庡仠浣忥紝绛夌湡姝ｅ氨缁啀杩涘叆涓嬩竴闃舵銆?
        bool clientMapWait = ShipStatus.Instance == null
            && !client.ShipLoadingAsyncHandle.IsValid()
            && !client.AmHost;

        if (clientMapWait)
        {
            if (_mapWaitStart < 0f) _mapWaitStart = Time.realtimeSinceStartup;
        }
        else
        {
            _mapWaitStart = -1f;   // 绂诲紑璇ラ樁娈靛氨澶嶄綅锛屼笅娆￠噸鏂拌鏃?
        }

        if (ShipStatus.Instance == null || client.ShipLoadingAsyncHandle.IsValid())
        {
            bool asyncLoading = client.ShipLoadingAsyncHandle.IsValid();
            if (asyncLoading)
            {
                float p = 0f;
                try { p = client.ShipLoadingAsyncHandle.PercentComplete; } catch { }
                if (float.IsNaN(p) || p < 0f) p = 0f;
                if (p > 1f) p = 1f;
                percent = 30f + 40f * p;
                text = AmethystText.T("LoadingShipPercent", Mathf.RoundToInt(p * 100f));
            }
            else if (!client.AmHost)
            {
                // 鎴愬憳绔及绠楋細30% 鈫?68%
                float elapsed = _mapWaitStart >= 0f ? Time.realtimeSinceStartup - _mapWaitStart : 0f;
                float estimate = 30f + 38f * Mathf.Clamp01(elapsed / ClientMapRampSeconds);
                float fromGame = _gamePercent >= 0f ? 30f + 38f * Mathf.Clamp01(_gamePercent / 100f) : 0f;
                percent = Mathf.Max(estimate, fromGame);
                // 鏂囧瓧鍙ｅ緞涓庢埧涓荤淇濇寔涓€鑷达細鏄剧ず"鍦板浘鍔犺浇杩涘害"0~100锛堝搴旇繘搴︽潯鐨?30%~70%锛?
                text = AmethystText.T("LoadingShipPercent", Mathf.RoundToInt((percent - 30f) / 40f * 100f));
            }
            else
            {
                text = AmethystText.T("SpawningShip");
                percent = 30f;
            }
            return;
        }

        // 鍦板浘宸插氨缁細鍏堢瓑鍏朵粬瀹㈡埛绔姞杞藉畬鎴愶紝鍐嶇瓑鎴夸富鍒嗛厤瑙掕壊銆?
        // IsReady 鍙湁鎴夸富绔細鏀跺埌鍏ㄩ儴瀹㈡埛绔殑灏辩华娑堟伅锛屾墍浠ヤ汉鏁扮粺璁″彧鍦ㄦ埧涓荤灞曠ず銆?
        if (client.AmHost)
        {
            CountClients(out int readyClients, out int totalClients);
            if (totalClients > 0 && readyClients < totalClients)
            {
                text = AmethystText.T("WaitingForOtherPlayers", readyClients, totalClients);
                percent = 72f + 12f * readyClients / totalClients;
                return;
            }
        }

        CountRoles(out int assignedPlayers, out int totalPlayers);
        if (totalPlayers > 0 && assignedPlayers < totalPlayers)
        {
            if (assignedPlayers == 0)
            {
                // 闈炴埧涓荤鎷夸笉鍒板埆浜虹殑灏辩华鐘舵€侊紝杩欓噷鍙鏄庡湪绛夛紝涓嶇紪閫犱汉鏁般€?
                text = client.AmHost
                    ? AmethystText.T("AssigningRoles")
                    : AmethystText.T("WaitingForOtherPlayers_2");
                percent = 85f;
                return;
            }

            text = AmethystText.T("AssigningRoles_2", assignedPlayers, totalPlayers);
            percent = 85f + 12f * assignedPlayers / totalPlayers;
            return;
        }

        if (IntroCutscene.Instance != null)
        {
            text = AmethystText.T("PlayingIntro");
            percent = 97f;
            return;
        }

        text = AmethystText.T("AlmostReady");
        percent = 97f;
    }

    private static bool IsGameGone()
    {
        try
        {
            AmongUsClient client = AmongUsClient.Instance;
            if (client == null) return true;
            return client.GameState == InnerNetClient.GameStates.Ended
                || client.GameState == InnerNetClient.GameStates.NotJoined;
        }
        catch { return true; }
    }

    private static bool IsLoadingFinished()
    {
        try
        {
            AmongUsClient client = AmongUsClient.Instance;
            if (client == null) return true;
            if (client.GameState != InnerNetClient.GameStates.Started) return false;

            // 涓嶈兘鎶婂紑鍦哄姩鐢诲綋鎴愨€滃姞杞界粨鏉熲€濄€傚畠鍑虹幇鏃堕€氬父鍙槸杩涘叆浜嗗師鐗堝紑鍦烘祦绋嬨€?
            if (IntroCutscene.Instance != null) return false;

            if (ShipStatus.Instance == null || client.ShipLoadingAsyncHandle.IsValid()) return false;

            CountRoles(out int assignedPlayers, out int totalPlayers);
            if (totalPlayers <= 0) return Time.realtimeSinceStartup - _loadingStartedAt >= 3f;
            return assignedPlayers >= totalPlayers;
        }
        catch { return true; }
    }

    // 涓ユ牸鎸?ClientData.IsReady 缁熻锛岄伩鍏嶆妸鈥滄暟鎹凡鍚屾鈥濊鍒や负鈥滃凡鍔犺浇瀹屾垚鈥濄€?
    private static void CountClients(out int readyClients, out int totalClients)
    {
        readyClients = 0;
        totalClients = 0;

        try
        {
            AmongUsClient net = AmongUsClient.Instance;
            var clients = net != null ? net.allClients : null;
            if (clients == null) return;

            int localId = net.ClientId;
            for (int i = 0; i < clients.Count; i++)
            {
                ClientData client = clients[i];
                if (client == null || client.Character == null) continue;
                totalClients++;

                // 鏈湴瀹㈡埛绔殑 IsReady 鍙湁鎴夸富绔細缃綅锛涜兘璧板埌杩欎竴姝ヨ鏄庢湰鍦板凡鍔犺浇瀹屾瘯銆?
                if (client.IsReady || client.Id == localId) readyClients++;
            }
        }
        catch
        {
            readyClients = 0;
            totalClients = 0;
        }
    }

    private static void CountRoles(out int assignedPlayers, out int totalPlayers)
    {
        assignedPlayers = 0;
        totalPlayers = 0;

        try
        {
            var all = PlayerControl.AllPlayerControls;
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerControl pc = all[i];
                if (pc == null || pc.isDummy) continue;
                totalPlayers++;
                if (pc.roleAssigned) assignedPlayers++;
            }
        }
        catch
        {
            assignedPlayers = 0;
            totalPlayers = 0;
        }
    }

    private static void SetLoadingPercent(float percent, string text)
    {
        try
        {
            var bar = LoadingBarManager.Instance != null ? LoadingBarManager.Instance.loadingBar : null;
            if (bar == null) return;
            _allowLoadingBarUpdate = true;
            try
            {
                bar.SetLoadingPercent(percent, StringNames.None);
            }
            finally
            {
                _allowLoadingBarUpdate = false;
            }
            if (bar.loadingText != null)
            {
                bar.loadingText.gameObject.SetActive(true);
                bar.loadingText.enabled = true;
                bar.loadingText.SetText(text);
            }
        }
        catch { }
    }
}

[HarmonyPatch(typeof(LoadingBarManager), "SetLoadingPercent")]
internal static class AmethystLoadingBarManagerSetPatch
{
    [HarmonyPrefix]
    private static bool Prefix(float percent)
    {
        // 涓嶆槸鎴戜滑鐨勮皟鐢紙鎴戜滑鐨勮皟鐢ㄤ細鎶?_allowLoadingBarUpdate 缃湡锛夛紳 娓告垙鑷繁涓婃姤鐨勮繘搴︼紝
        // 璁板綍涓嬫潵澶囩敤锛氭垚鍛樼鍦板浘闃舵鎷夸笉鍒板彞鏌勬椂锛屾父鎴忕殑鍊兼瘮鎴戜滑鐨勪及绠楁洿鐪熷疄銆?
        if (!AmethystBetterLoadingPatch.AllowLoadingBarUpdate)
            AmethystBetterLoadingPatch.NoteGamePercent(percent);

        return !AmethystBetterLoadingPatch.Active || AmethystBetterLoadingPatch.AllowLoadingBarUpdate;
    }
}

[HarmonyPatch(typeof(LoadingBarManager), "ToggleLoadingBar")]
internal static class AmethystLoadingBarManagerTogglePatch
{
    [HarmonyPrefix]
    private static bool Prefix()
    {
        return !AmethystBetterLoadingPatch.Active;
    }
}

// ===== 鏈嶈淇濆瓨 UI 宸茬Щ鑷崇嫭绔嬫枃浠?=====
// 鍘熷厛杩欓噷鏀?AmethystOutfitSaverMenuPatch / AmethystOutfitSaverTabPatch锛?
// 绗笁鍗佷簲杞浘鏁翠綋绉婚櫎锛岀鍥涘崄涓€杞寜闇€姹傛仮澶嶅苟杩佺Щ鍒帮細
//   src\Patches\AmethystOutfitSaverUI.cs
//     - AmethystOutfitPrefabCachePatch锛堢紦瀛?inventoryButton 妯℃澘锛屾寕 MainMenuManager.Start锛?
//     - AmethystOutfitSaverUIPatch锛堝線浠撳簱椤?PlayerTab 娉ㄥ叆 6 涓璁炬寜閽級
// 鎭㈠鏃朵繚鐣欎簡 Postfix 娉ㄥ叆銆佹ā鏉胯法鍦烘櫙瀛樻椿銆佺湡娲绘牎楠岀瓑鍔犲浐锛?
// 骞舵妸 Destroy 鏀逛负 SetActive(false) 浠ラ伩鍏嶆瘡甯?NRE銆?
// 鏈嶈棰勮鐨勫瓨鍙栭€昏緫浠嶄负 Core\AmethystOutfitSaver.cs锛岄厤缃」 Other.OutfitSaver銆?

// ===== 瑙ｉ攣鎵€鏈夌毊鑲わ紙鍙傝€?AUnlocker CosmeticsPatches / MalumMenu CosmeticsUnlocker锛?====
[HarmonyPatch(typeof(HatManager), "Initialize")]
internal static class AmethystUnlockCosmeticsPatch
{
    public static void Postfix(HatManager __instance)
    {
        if (AmethystCompat.OtherUnlockSkins || !AmethystConfig.UnlockAllCosmetics.Value || __instance == null) return;
        try
        {
            foreach (var bundle in __instance.allBundles) bundle.Free = true;
            foreach (var featuredBundle in __instance.allFeaturedBundles) featuredBundle.Free = true;
            foreach (var featuredCube in __instance.allFeaturedCubes) featuredCube.Free = true;
            foreach (var featuredItem in __instance.allFeaturedItems) featuredItem.Free = true;
            foreach (var hat in __instance.allHats) hat.Free = true;
            foreach (var nameplate in __instance.allNamePlates) nameplate.Free = true;
            foreach (var pet in __instance.allPets) pet.Free = true;
            foreach (var skin in __instance.allSkins) skin.Free = true;
            foreach (var starBundle in __instance.allStarBundles) starBundle.price = 0;
            foreach (var visor in __instance.allVisors) visor.Free = true;
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerPurchasesData), "GetPurchase")]
internal static class AmethystUnlockCosmeticsPurchasePatch
{
    // 鎵€鏈夌墿鍝佽涓哄凡璐拱锛涙琛ヤ竵鎸佺画鐢熸晥锛屼腑閫斿紑鍚篃绔嬪嵆瑙ｉ攣绌挎埓
    public static bool Prefix(ref bool __result)
    {
        if (AmethystCompat.OtherUnlockSkins || !AmethystConfig.UnlockAllCosmetics.Value) return true;
        __result = true;
        return false;
    }
}

// ===== 闃叉鎴夸富浣跨敤鍏ㄥ憳璐﹀彿灏佺澶栨寕锛堥槻寰?Hydra TempBanAll锛?====
[HarmonyPatch(typeof(AmongUs.Data.Player.PlayerBanData), nameof(AmongUs.Data.Player.PlayerBanData.BanPoints), MethodType.Setter)]
internal static class AmethystBypassDisconnectPenaltyPatch
{
    private static readonly System.Collections.Generic.List<float> StartGameTimestamps = new System.Collections.Generic.List<float>();
    private static float _lastWarningTime = -1f;

    /// <summary>
    /// 妫€娴嬫埧涓绘槸鍚﹀湪鐭椂闂村唴鐙傚埛 StartGame (渚嬪 0.05绉掑唴鍙戦€?3 娆′互涓?
    /// </summary>
    internal static bool CheckHostRapidStart()
    {
        try
        {
            bool preventOn = AmethystConfig.BypassDisconnectPenalty != null && AmethystConfig.BypassDisconnectPenalty.Value;
            bool detectOn = AmethystConfig.DetectHostTempBan != null && AmethystConfig.DetectHostTempBan.Value;
            if (!preventOn && !detectOn) return false;
            if (AmongUsClient.Instance == null || AmongUsClient.Instance.AmHost) return false; // 鑷繁鏄埧涓讳笉鍒ゅ畾

            float now = Time.realtimeSinceStartup;
            StartGameTimestamps.Add(now);

            // 娓呯悊瓒呰繃 0.1s 鐨勬棫璁板綍
            while (StartGameTimestamps.Count > 0 && now - StartGameTimestamps[0] > 0.1f)
            {
                StartGameTimestamps.RemoveAt(0);
            }

            // 缁熻 0.05 绉掑唴鐨勬鏁?
            int countInWindow = 0;
            for (int i = 0; i < StartGameTimestamps.Count; i++)
            {
                if (now - StartGameTimestamps[i] <= 0.05f) countInWindow++;
            }

            if (countInWindow >= 3)
            {
                // 鍒ゅ畾涓哄叏鍛樺皝绂佸鎸傛敾鍑伙細鍙戝嚭璀﹀憡涓庢棩蹇楄褰曪紝浣嗕笉涓诲姩绂诲紑鎴块棿
                if (detectOn && now - _lastWarningTime > 5f)
                {
                    _lastWarningTime = now;
                    string msg = AmethystText.T("TempBanAllDetected");
                    AmethystToast.Push(AmethystText.T("Guard"), msg, 10.0f, AmethystNotifyKind.Danger);
                    AmethystEventLog.Add(msg, AmethystNotifyKind.Danger, AmethystEventCat.Other);
                    AmethystPlugin.Logger?.LogWarning($"[AntiCheat] {msg} (Rapid StartGame: {countInWindow} in 0.05s)");
                }
                return preventOn; // 寮€鍚槻姝㈡椂涓㈠純鍚庣画鎭舵剰鍚姩
            }
        }
        catch { }
        return false;
    }

    public static bool Prefix(ref float value)
    {
        if (!AmethystConfig.BypassDisconnectPenalty.Value) return true;
        try
        {
            if (!(bool)(UnityEngine.Object)AmongUsClient.Instance
                || AmongUsClient.Instance.NetworkMode != NetworkModes.OnlineGame)
                return true;
        }
        catch { return true; }

        // ⚠ 第 241 轮修复：这里原来 `value = 0f; return false;`
        //   —— `return false` 会**跳过原 setter**，等于"忽略这次写入"，
        //   而不是"把 0 写进去"。若 banPoints 已经是正数，它就会一直留着。
        //   现在改为把值改成 0 后**执行原 setter**，真正清零。
        value = 0f;
        return true;
    }
}

// 兜底：任何读取路径都直接给出"无惩罚"。
// 参考 Hydra `BypassDisconnectPenalty`（改 `BanMinutesLeft` getter）
// 与 FinalSuspect / OnyxMenu（同时改 `IsBanned` / `BanMinutes`）：
// 只清 setter 不够，因为游戏可能直接写 `banPoints` 字段而不走属性。
[HarmonyPatch(typeof(AmongUs.Data.Player.PlayerBanData), nameof(AmongUs.Data.Player.PlayerBanData.BanMinutesLeft), MethodType.Getter)]
internal static class AmethystBypassDisconnectPenaltyMinutesLeftPatch
{
    private static void Postfix(AmongUs.Data.Player.PlayerBanData __instance, ref int __result)
    {
        if (!AmethystConfig.BypassDisconnectPenalty.Value) return;
        try { __instance.banPoints = 0f; } catch { }
        __result = 0;
    }
}

[HarmonyPatch(typeof(AmongUs.Data.Player.PlayerBanData), nameof(AmongUs.Data.Player.PlayerBanData.BanMinutes), MethodType.Getter)]
internal static class AmethystBypassDisconnectPenaltyMinutesPatch
{
    private static void Postfix(AmongUs.Data.Player.PlayerBanData __instance, ref float __result)
    {
        if (!AmethystConfig.BypassDisconnectPenalty.Value) return;
        try { __instance.banPoints = 0f; } catch { }
        __result = 0f;
    }
}

[HarmonyPatch(typeof(AmongUs.Data.Player.PlayerBanData), nameof(AmongUs.Data.Player.PlayerBanData.IsBanned), MethodType.Getter)]
internal static class AmethystBypassDisconnectPenaltyIsBannedPatch
{
    private static void Postfix(AmongUs.Data.Player.PlayerBanData __instance, ref bool __result)
    {
        if (!AmethystConfig.BypassDisconnectPenalty.Value) return;
        try { __instance.banPoints = 0f; } catch { }
        __result = false;
    }
}

// 鎷︽埅鎭舵剰閲嶅鐨?CoStartGame 鍗忕▼锛堟嫤鎴埧涓?0.05s 鍐呭ぇ浜?3 娆＄殑 StartGame 娑堟伅锛?
[HarmonyPatch(typeof(AmongUsClient), "CoStartGame")]
internal static class AmethystBlockRapidStartGamePatch
{
    private static float _lastGameStartTime = -1f;

    [HarmonyPrefix]
    private static bool Prefix()
    {
        // 1. 楂橀妫€娴?(0.05绉掑唴澶氭寮€灞€)
        if (AmethystBypassDisconnectPenaltyPatch.CheckHostRapidStart())
        {
            return false; // 涓㈠純骞堕樆姝㈡伓鎰忕殑骞跺彂寮€灞€鍗忕▼鍚姩
        }

        // 2. 鐘舵€侀槻骞跺彂锛氬鏋滃綋鍓嶅鎴风宸茬粡澶勪簬寮€灞€娴佺▼涓紙鏈柇绾裤€佹湭鍥炲埌澶у巺锛夛紝鐩存帴闃绘柇鍚庣画澶氫綑鐨勫紑灞€
        try
        {
            if (AmongUsClient.Instance != null && !AmongUsClient.Instance.AmHost)
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastGameStartTime < 2f)
                {
                    // 2绉掑唴閲嶅寮€灞€鍏ㄩ儴鏃犳潯浠堕樆鏂紝娑堥櫎鏁扮櫨涓崗绋嬮€犳垚鐨勫崱椤?
                    return false;
                }
                _lastGameStartTime = now;
            }
        }
        catch { }

        return true;
    }
}

// ===== 寮哄埗鏄剧ず鑱婂ぉ =====
[HarmonyPatch]
internal static class AmethystChatVisibilityState
{
    internal static bool ShouldShow()
    {
        try
        {
            if (AmethystConfig.VisualAlwaysShowChat.Value) return true;
            if (HudManager.Instance == null) return false;
            if (LobbyBehaviour.Instance != null || MeetingHud.Instance != null || ExileController.Instance != null || IntroCutscene.Instance != null)
                return true;

            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null) return false;
            return local.Data.IsDead;
        }
        catch { return false; }
    }
}

[HarmonyPatch(typeof(ChatController), "SetVisible")]
internal static class AmethystAlwaysChatVisiblePatch
{
    public static void Prefix(ref bool visible)
    {
        visible = AmethystChatVisibilityState.ShouldShow();
    }
}

[HarmonyPatch(typeof(ChatController), "Update")]
internal static class AmethystAlwaysChatLayoutPatch
{
    public static void Postfix(ChatController __instance)
    {
        // 绗?199 杞細骞跺叆 ChatAux 璁￠噺銆?
        int p = AmethystPerfProbe.Begin(Section.S_ChatAux);
        try
        {
            if (__instance == null || __instance.quickChatButton == null || __instance.openKeyboardButton == null) return;
        }
        catch { }
        finally { AmethystPerfProbe.End(p, Section.S_ChatAux); }
    }
}

[HarmonyPatch(typeof(MatchInfoHudButton), "Update")]
internal static class AmethystChatOverlapFixPatch
{
    public static bool Prefix(MatchInfoHudButton __instance)
    {
        try
        {
            if (__instance == null || __instance.aspectPosition == null) return true;

            if (AmethystChatVisibilityState.ShouldShow())
                __instance.aspectPosition.DistanceFromEdge = MatchInfoHudButton.adjustedDistanceFromEdge;
            else
                __instance.aspectPosition.DistanceFromEdge = MatchInfoHudButton.defaultDistanceFromEdge;

            return false;
        }
        catch { return true; }
    }
}

internal static class AmethystAlwaysChatState
{
    internal static void Tick(HudManager __instance)
    {
        try
        {
            if (__instance != null && __instance.Chat != null)
            {
                ((Component)__instance.Chat).gameObject.SetActive(AmethystChatVisibilityState.ShouldShow());
            }
        }
        catch { }
    }
}

// ===== 鏃犳父鎴忕粨鏉燂紙鏃犺儨鍒╂潯浠讹級=====
//
// 鈿?鍙嫤 `LogicGameFlowNormal.CheckEndCriteria` 鏄笉澶熺殑 鈥斺€?娓告垙鏈?*涓夋潯**鐙珛鐨勭粨鏉熼€氳矾锛?
//   鈶?缁忓吀灞€鐨勮儨璐熷垽瀹? 鈫?LogicGameFlowNormal.CheckEndCriteria
//   鈶?韬茬尗鐚殑鑳滆礋鍒ゅ畾  鈫?LogicGameFlowHnS.CheckEndCriteria锛堟棫瀹炵幇婕忎簡杩欐潯 鈬?韬茬尗鐚噷鏃犳晥锛?
//   鈶?鑸瑰憳鍋氬畬鍏ㄩ儴浠诲姟  鈫?GameManager.CheckTaskCompletion锛堟棫瀹炵幇涔熸紡浜?鈬?浠诲姟鍨嬭儨鍒╂棤鏁堬級
//   鍙傝€冿細OnyxMenu `src\Patches\GameplayPatches.cs` 绗?46/52 琛屾嫤鍓嶄袱鏉★紝
//         Elysium `cheats\TaskCompletionPatch.cs` 绗?42 琛屾嫤绗笁鏉°€?
internal static class AmethystNoWinConditions
{
    internal static bool On
    {
        get
        {
            try
            {
                if (AmethystConfig.NoWinConditions == null || !AmethystConfig.NoWinConditions.Value) return false;
                return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
            }
            catch { return false; }
        }
    }
}

[HarmonyPatch(typeof(LogicGameFlowNormal), "CheckEndCriteria")]
internal static class AmethystNoWinConditionsPatch
{
    public static bool Prefix() => !AmethystNoWinConditions.On;
}

[HarmonyPatch(typeof(LogicGameFlowHnS), "CheckEndCriteria")]
internal static class AmethystNoWinConditionsHnSPatch
{
    public static bool Prefix() => !AmethystNoWinConditions.On;
}

[HarmonyPatch(typeof(GameManager), "CheckTaskCompletion")]
internal static class AmethystNoWinConditionsTaskPatch
{
    public static bool Prefix(ref bool __result)
    {
        if (!AmethystNoWinConditions.On) return true;
        __result = false;
        return false;
    }
}
