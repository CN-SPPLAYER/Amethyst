using System;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using InnerNet;
using TMPro;
using UnityEngine;

namespace Amethyst.Patches;

// ===== 每名玩家一个的头顶覆盖层（第 180 轮：三个组件合并为一个）====//
//
// 【为什么要合并】
//   此前每个 `PlayerControl` 上挂着**三个** MonoBehaviour，各有自己的 LateUpdate：
//     · AmethystModClientTag   （Core\AmethystModClientDetection.cs）
//     · AmethystAliveRoleTag   （Patches\AmethystDeadRoleReveal.cs）
//     · AmethystRoleTag        （Patches\AmethystDeadRoleReveal.cs）
//   三者做的事高度同构：找到 `Names/NameText_TMP` → 克隆一个 TMP → 每帧比较内容、
//   不同才写、可见性翻转时才动 `enabled`、折行补偿变了才动 `transform`。
//
//   10 人局 = **30 个 LateUpdate / 帧**。每个都要过一遍 Unity 的 MonoBehaviour
//   派发 + 一次 `EnsureText()` 调用 + 一次 `PlayerId` 原生读取 + 字典查找 +
//   字符串比较。合并成一个之后是 10 个 —— 派发与状态判断的开销直接降到 1/3。
//
// 【数据来源不变】
//   所有"读原生 / 分配字符串 / 拼富文本"仍然由 `AmethystPlayerCacheStore` 的
//   错峰刷新算好（`AliveText` / `DeadText` / `LineExtra`），本组件每帧只做
//   纯托管比较，不跨 IL2CPP 边界、不分配字符串。
//
// 【渲染轮转】
//   「低负载模式」开启时（`AmethystPerformance.RenderStride > 1`），每名玩家
//   只在 `frame % stride == (id % stride)` 的帧上干活 —— 每人每 stride 帧刷新一次
//   （stride=3 时约 20Hz），文字本身只在内容变化时才真正写 TMP，肉眼看不出差别。
internal sealed class AmethystPlayerOverlay : MonoBehaviour
{
    private const float NameOffsetY = 0.30f;
    private const float ModTagOffsetY = 0.22f;

    private PlayerControl _player;

    // 原版名字 TMP：折行补偿（`textInfo.lineCount`，原生读取）要用它。
    private TextMeshPro _nameText;

    private TextMeshPro _modTag;
    private TextMeshPro _aliveTag;
    private TextMeshPro _deadTag;

    private int _createRetryAt;

    // ---- "已经写出去"的状态：每帧只做纯托管比较 ----
    private string _wMod = string.Empty;
    private string _wAlive = string.Empty;
    private string _wDead = string.Empty;
    private bool _vMod, _vAlive, _vDead;
    private bool _hasPos;
    private float _lastLineExtra;

    public AmethystPlayerOverlay(IntPtr ptr) : base(ptr) { }

    internal void Init(PlayerControl player)
    {
        _player = player;
        EnsureTexts();
    }

    private void EnsureTexts()
    {
        // 三个都建好了才退出；没建好时**不要每帧重试**（Find + GetComponent + Instantiate
        // 全是原生调用），限流到每 30 帧一次。
        if (_modTag != null && _aliveTag != null && _deadTag != null) return;
        if (_player == null) return;

        int frame = Time.frameCount;
        if (frame < _createRetryAt) return;
        _createRetryAt = frame + 30;

        try
        {
            Transform names = _player.gameObject.transform.Find("Names/NameText_TMP");
            if (names == null) return;

            TextMeshPro source = names.GetComponent<TextMeshPro>();
            if (source == null) return;

            // 缓存原版名字，供 NameLineExtra 精确读取行数（不再靠 parent 反查）。
            _nameText = source;

            _modTag ??= MakeTag(source, names, "AmethystModClientTag", ModTagOffsetY, 1.4f, false);
            _aliveTag ??= MakeTag(source, names, "AmethystAliveRoleTag", NameOffsetY, 1.6f, true);
            _deadTag ??= MakeTag(source, names, "AmethystRoleTag", NameOffsetY, 1.6f, true);
        }
        catch { }
    }

    private static TextMeshPro MakeTag(TextMeshPro source, Transform parent, string tagName, float y, float size, bool roleStyle)
    {
        TextMeshPro t = UnityEngine.Object.Instantiate(source, parent);
        t.name = tagName;
        t.transform.localPosition = new Vector3(0f, y, 0f);
        t.transform.localScale = Vector3.one;
        t.fontSize = size;
        if (roleStyle)
        {
            t.fontSizeMin = size;
            t.fontSizeMax = size;
            t.enableAutoSizing = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Overflow;
            t.alignment = TextAlignmentOptions.Center;
        }
        if (t.text != string.Empty) t.text = string.Empty;
        t.gameObject.SetActive(true);
        return t;
    }

    private void LateUpdate()
    {
        if (_player == null) return;

        int probe = AmethystPerfProbe.Begin(Section.S_Overlay);
        try { LateUpdateInner(); }
        finally { AmethystPerfProbe.End(probe, Section.S_Overlay); }
    }

    private void LateUpdateInner()
    {
        EnsureTexts();
        if (_modTag == null && _aliveTag == null && _deadTag == null) return;

        try
        {
            byte id;
            try { id = _player.PlayerId; } catch { return; }
            if (id >= 254) return;

            AmethystPlayerCacheStore.TryGet(id, out AmethystPlayerCache c);

            // 第 182 轮：与名字/色盲文字共用同一份就地刷新。
            // ⚠ 必须放在渲染轮转**之前** —— 轮转会 return 掉 2/3 的帧，
            //   放在之后会让身份标记比名字晚最多 3 帧才出现。
            if (c != null) AmethystPlayerCacheStore.RefreshIfDirty(c);

            // ---- 渲染轮转（低负载模式）----
            int stride = AmethystPerformance.RenderStride;
            if (stride > 1 && (Time.frameCount % stride) != (id % stride)) return;

            // ---- ① 模组客户端标签 ----
            UpdateModTag(id);

            // ---- ② 活着身份 / ③ 死后身份：两者互斥，共享同一套折行补偿与位置 ----
            UpdateRoleTags(c, id);
        }
        catch { }
    }

    // 模组客户端识别 + 版本号（大厅中在头顶显示 `Ame{Version} 用户` / 其它识别到的模组）。
    private void UpdateModTag(byte id)
    {
        if (_modTag == null) return;

        bool visible = AmethystPlayerCacheStore.InLobby;
        string want = string.Empty;

        if (visible)
        {
            bool isAme = AmethystModClientDetection.IsDetectedId(id);
            string otherMods = null;
            if (_player != null)
            {
                otherMods = AmethystKnownModDetection.GetDetectedModName(_player);
            }

            if (isAme)
            {
                string ver = AmethystModClientDetection.GetVersion(id);
                // 自己返回本机插件版本；其它玩家是收到他们的 probe/reply 时记上的。
                string line = string.IsNullOrEmpty(ver)
                    ? AmethystText.T("AmeUserTag")
                    : AmethystText.T("AmeUserVerTag", ver);
                if (!string.IsNullOrEmpty(otherMods))
                    want = $"<color=#b554ff>{line}</color> + <color=#ff4a62>[{otherMods}]</color>";
                else
                    want = $"<color=#b554ff>{line}</color>";
            }
            else if (!string.IsNullOrEmpty(otherMods))
            {
                want = $"<color=#ff4a62>[{otherMods}]</color>";
            }
            else
            {
                visible = false;
            }
        }

        if (!string.Equals(want, _wMod, StringComparison.Ordinal))
        {
            _wMod = want;
            try { _modTag.text = want; } catch { }
        }
        if (visible != _vMod)
        {
            _vMod = visible;
            try { _modTag.enabled = visible; } catch { }
        }
    }

    [HideFromIl2Cpp]
    private void UpdateRoleTags(AmethystPlayerCache c, byte id)
    {
        // 身份文本来自错峰快照（每人每 N 帧算一次），这里零原生读取。
        // 可见性规则已在 ComputeIdentityTexts 中精确计算，此处直接读取计算好的 AliveText / DeadText。
        string alive = c != null ? c.AliveText : string.Empty;
        string dead = c != null ? c.DeadText : string.Empty;

        // 会议身份标记与身份显示**彼此独立**：只要有标记就显示，
        // 不受 ShowSelfRole / ShowRolesWhenDead 等开关影响（本地标记，只有自己看得到）。
        string mark = AmethystRoleTagStore.Suffix(id);
        if (!string.IsNullOrEmpty(mark))
        {
            string m = mark.Trim();
            if (!string.IsNullOrEmpty(dead)) dead = dead + m;
            else if (!string.IsNullOrEmpty(alive)) alive = alive + m;
            else dead = m;
        }

        bool aliveVisible = !string.IsNullOrEmpty(alive);
        bool deadVisible = !string.IsNullOrEmpty(dead);

        // ---- 内容：变了才写 ----
        if (_aliveTag != null && !string.Equals(alive, _wAlive, StringComparison.Ordinal))
        {
            _wAlive = alive;
            try { _aliveTag.text = alive; } catch { }
        }
        if (_deadTag != null && !string.Equals(dead, _wDead, StringComparison.Ordinal))
        {
            _wDead = dead;
            try { _deadTag.text = dead; } catch { }
        }

        // ---- 可见性：只在翻转时动 enabled，之后完全空转 ----
        if (aliveVisible != _vAlive)
        {
            _vAlive = aliveVisible;
            try { if (_aliveTag != null) _aliveTag.enabled = aliveVisible; } catch { }
        }
        if (deadVisible != _vDead)
        {
            _vDead = deadVisible;
            try { if (_deadTag != null) _deadTag.enabled = deadVisible; } catch { }
        }

        if (!aliveVisible && !deadVisible) return;

        // ---- 位置：只有折行补偿变了才重写 Transform ----
        // `LineExtra` 由快照错峰算好（每 N 帧一次），本帧不再读原生 `textInfo`。
        float extra = c != null ? c.LineExtra : AmethystDeadRoleReveal.NameLineExtra(_nameText);
        if (_hasPos && extra == _lastLineExtra) return;

        _lastLineExtra = extra;
        _hasPos = true;
        float y = NameOffsetY + extra;
        try
        {
            if (_aliveTag != null) _aliveTag.transform.localPosition = new Vector3(0f, y, 0f);
            if (_deadTag != null) _deadTag.transform.localPosition = new Vector3(0f, y, 0f);
        }
        catch { }
    }

    private const string ModTagText = "<color=#b554ff>♥ Amethyst ♥</color>";
}

[HarmonyPatch(typeof(PlayerControl), "Awake")]
internal static class AmethystPlayerOverlayAttachPatch
{
    private static void Postfix(PlayerControl __instance)
    {
        try
        {
            if (__instance == null) return;

            // 第 181 轮：玩家对象一建出来就立刻建快照并置脏，
            // 这样进大厅的**第一帧**就有名字与颜色，
            // 不用等最多 20 帧的 SyncPlayers 轮询 + 20 帧的错峰槽位。
            AmethystPlayerCacheStore.OnPlayerAwake(__instance);

            if (__instance.gameObject.GetComponent<AmethystPlayerOverlay>() == null)
                __instance.gameObject.AddComponent<AmethystPlayerOverlay>().Init(__instance);
        }
        catch { }
    }
}

// ===== 第 181 轮：外观突变事件 → 快照立即刷新 =====
//
// 错峰刷新（每人每 N 帧一次）在稳态下很省，但**状态突变后的那个窗口期**里
// 快照还是旧值：用户实测到「刚进大厅名字有一两帧没颜色」
// 与「变形后有一两帧颜色和名字都不渲染」。
//
// 这里不改刷新频率（那会把省下来的开销全还回去），而是由「谁变了」这个事件
// 触发一次立即刷新 —— 一局里只发生几次，代价可以忽略。
internal static class AmethystOutfitChangeWatcher
{
    // 变形者 / 幻影 / 蘑菇混合：都会改写**外观类型**与**当前身体色**，
    // 影响到自己（伪装名与伪装色）与他人的可见性判定。
    [HarmonyPatch(typeof(PlayerControl), "HandleRpc")]
    private static class RpcPatch
    {
        private static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] byte callId)
        {
            try
            {
                switch ((RpcCalls)callId)
                {
                    case RpcCalls.Shapeshift:
                    case RpcCalls.CheckShapeshift:
                    case RpcCalls.RejectShapeshift:
                    case RpcCalls.StartVanish:
                    case RpcCalls.StartAppear:
                    case RpcCalls.CheckVanish:
                    case RpcCalls.CheckAppear:
                    case RpcCalls.SetColor:
                    case RpcCalls.SetHatStr:
                    case RpcCalls.SetSkinStr:
                    case RpcCalls.SetVisorStr:
                    case RpcCalls.SetPetStr:
                    case RpcCalls.SetNamePlateStr:
                        AmethystPlayerCacheStore.MarkAllDirty();
                        break;
                }
            }
            catch { }
        }
    }

    // 蘑菇混合：混装开始 / 复原。同一时刻影响全体玩家。
    [HarmonyPatch(typeof(PlayerControl), "MixUpOutfit")]
    private static class MixUpPatch
    {
        private static void Postfix() => AmethystPlayerCacheStore.MarkAllDirty();
    }

    [HarmonyPatch(typeof(PlayerControl), "FixMixedUpOutfit")]
    private static class FixMixUpPatch
    {
        private static void Postfix() => AmethystPlayerCacheStore.MarkAllDirty();
    }
}

// ===== 第 182 轮：游戏重建文本的那一刻置脏 =====
//
// 【为什么是这两个方法】
//   色盲文字有**两个**写入源：
//     ① `AmethystColorBlindTextPatch` 挂在 `CosmeticsLayer.GetColorBlindText()` 上 ——
//        游戏每次调它就当场拿到我们染好的字符串，**时间点由游戏决定**；
//     ② 我们的 LateUpdate（从快照的 `PendingColorBlindId` 写）。
//   名字只有 ②。
//   ⇒ 只要 ① 先于 ② 发生，那一帧就是「色盲文字有颜色、名字没有」——
//     用户实测到的正是这个，而且**试过几轮都没根治**，因为病根是这个双写入源。
//
//   修法（照参考模组的做法：事件驱动 + 单一数据源）：
//   `CosmeticsLayer.SetName(string)` 与 `SetColorBlindColor(int)` 正是游戏
//   **重建这两个文本的时刻**（已用 dnSpy 核实两者都存在）。在它们之后置脏，
//   我们的 LateUpdate 就在**同一帧**把名字补上 ⇒ 两者同帧生效。
//
//   为什么用 `MarkAllDirty()` 而不是精确定位那一个玩家：
//     `CosmeticsLayer` 上没有 `myPlayer` 引用（已核实），反查要遍历
//     `AllPlayerControls`。而这些事件一局只发生几次（进场/改名/变色/伪装），
//     且 `MarkAllDirty` 内部已做**同帧合并** ⇒ 进房时 10 次 `SetName` 只触发一次全量刷新。
internal static class AmethystTextRebuildWatcher
{
    // ⚠ 必须**精确指定参数类型**。`CosmeticsLayer` 上有两个 `SetName` 重载：
    //       public void SetName(string)
    //       public void SetName(string, Vector3, Color, float)
    //   只给名字会报 `HarmonyException: Ambiguous match`，
    //   补丁被 `PatchAllResilient` 跳过（日志里能看到 Skipped patch）—— 静默失效。
    //   写 `new Type[0]` 也不行：那是"匹配零参数重载"，同样找不到目标。
    [HarmonyPatch(typeof(CosmeticsLayer), "SetName", new Type[] { typeof(string) })]
    private static class SetNamePatch
    {
        private static void Postfix() => AmethystPlayerCacheStore.MarkAllDirty();
    }

    [HarmonyPatch(typeof(CosmeticsLayer), "SetColorBlindColor")]
    private static class SetColorBlindPatch
    {
        private static void Postfix() => AmethystPlayerCacheStore.MarkAllDirty();
    }
}

// ===== 第 184 轮：玩家进出房间时置脏 =====
//
// 参考 EHR `Patches\PlayerJoinAndLeftPatch.cs:662`：玩家离开时
// `Utils.DirtyName.Add(PlayerControl.LocalPlayer.PlayerId)`，
// 用来强制名字在低负载跳过之外重算一次。
//
// 我们这边同样需要：有人进/出房间时，**在场列表**与所有玩家的名字/身份都要重算，
// 否则会出现「大厅里别人的名字不渲染 / 渲染的是旧数据」。
internal static class AmethystLobbyNameWatcher
{
    [HarmonyPatch(typeof(AmongUsClient), "OnPlayerJoined")]
    private static class JoinedPatch
    {
        private static void Postfix()
        {
            AmethystPlayerCacheStore.MarkAllDirty();
            AmethystPlayerCacheStore.ForceResync();
        }
    }

    // ⚠ 这里**不声明** `ClientData` 参数：`OnPlayerLeft` 的参数类型在
    //   `InnerNet.ClientData` 与程序集里另一个同名类型之间容易在 Harmony 的
    //   IL 编译阶段炸掉（实测报 `IL Compile Error`，补丁被静默跳过）。
    //   而且残留条目本来就由 `SyncPlayers` 的「不在 AllPlayerControls 里就清掉」兜底，
    //   不依赖这里的清理。
    [HarmonyPatch(typeof(AmongUsClient), "OnPlayerLeft")]
    private static class LeftPatch
    {
        private static void Postfix()
        {
            AmethystPlayerCacheStore.MarkAllDirty();
            AmethystPlayerCacheStore.ForceResync();
        }
    }
}
