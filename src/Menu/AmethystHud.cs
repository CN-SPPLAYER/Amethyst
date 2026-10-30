using Amethyst.Patches;
using InnerNet;
using UnityEngine;

namespace Amethyst;

public sealed class AmethystHud : MonoBehaviour
{
    // 主界面优化低频节流（0.5s），避免每帧做 GameObject.Find
    private float _mainMenuNext;
    private float _perfNext;   // 每帧功能节流到 0.1s（性能）
    // 复盘信息：结算阶段低频抓取节流
    private float _recapNext;
    // ⚠ 第 170 轮：对局中快照需要**独立**计时器。原来复用 `_perfNext`，
    //   而上面的 0.1s 块刚刚把 `_perfNext` 设成 `now + 0.1`，于是
    //   `Time.unscaledTime >= _perfNext` 恒为 false —— 快照从未执行过。
    private float _recapSyncNext;

    public void Update()
    {
        int probe = AmethystPerfProbe.Begin(Section.S_Hud);
        try { UpdateInner(); }
        finally { AmethystPerfProbe.End(probe, Section.S_Hud); }

        // 把本帧各段的测量结果并入汇总窗口（探针关闭时是一次空调用）。
        try { AmethystPerfProbe.Flush(); } catch { }
    }

    private void UpdateInner()
    {
        // 每秒更新调度器的帧计数（参考 EHR PerSecondUpdateScheduler.OnFixedUpdate）。
        // 必须每帧推进 —— 否则槽位不轮转，所有 ShouldRun 都会卡死在同一个槽位。
        try { AmethystUpdateScheduler.OnTick(); } catch { }

        // 强制成年：把账号的 KWS 未成年标记清掉（内部 0.25s 节流）。
        try { Patches.AmethystAccountUnlock.Tick(); } catch { }

        // 搜索房间列表的滚动吸附/裁剪**不在这里** —— 见本文件末尾 `LateUpdate`。
        // （必须晚于 `Scroller.Update` 改 `Inner.localPosition`，否则会有一帧半行露在列表外。）

        // ⚠ 第 179 轮性能重写的地基：**必须最先跑**。
        //   它每帧只读一次全局状态（AmongUsClient / ShipStatus / LobbyBehaviour / 本地玩家 / 开关），
        //   并把「每名玩家」的原生字段读取按 20 帧错峰刷新（任何一帧只碰 ~n/20 名玩家）。
        //   之后所有 LateUpdate 消费者（名字渲染 / 色盲文字 / 身份标签）都只做纯托管比较，
        //   稳态下不再跨 IL2CPP 边界、不再分配字符串。
        //
        //   Unity 保证「所有 Update 先于所有 LateUpdate」，所以放在这里能保证消费者读到本帧快照。
        try { AmethystPlayerCacheStore.Tick(); } catch { }

        // ⚠ 帧率统计必须**每帧**采样，绝不能放进任何节流/调度里！
        //
        //   第 176 轮我把这一句挪进了 `ShouldRun("ping")`（每秒一次）的块里，
        //   结果 `_frames` 从"每帧 +1"变成"每秒 +1"，算出来的帧率直接掉到个位数
        //   （用户实测卡在 3fps）。
        //   原因很简单：`AmethystBetterPingState.Tick()` 干的是
        //   「累加帧数 + 累加 deltaTime，每满 0.25s 算一次 FPS」——
        //   它本身就是**采样器**，采样频率被降到 1Hz，测出来的自然不是帧率。
        //   所以这一项永久排除在调度之外。
        try
        {
            if (AmethystConfig.BetterPingDisplay != null && AmethystConfig.BetterPingDisplay.Value)
                AmethystBetterPingState.Tick();
        }
        catch { }

        if (AmethystConfig.CopyCodeKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.CopyCodeKey.Value))
            CopyLobbyCode();

        // 这些原本是对 HudManager.Update / ModManager.LateUpdate / HudManager.Start 的 Harmony 补丁；
        // 那类补丁会让原生方法抛 NRE，改为在这里轮询。
        // ⚠ 性能（第 150 轮）：这几项以前是**每帧**执行（`Apply()` / `Tick()` 内部会做
        // 查找与遍历），用连点器 / 输入事件多的时候帧预算被吃掉，表现为明显卡顿。
        // 统一节流到 **0.1 秒**一次：视觉上完全看不出差别，开销降到 1/6。
        //
        // ⚠ 性能（第 176 轮）：0.1 秒节流只是"降低频率"，但这一块里的**所有**项目
        //   仍然挤在**同一帧**里跑完 —— 每 6 帧就出现一次明显的帧时间尖峰（周期性卡顿）。
        //   现在用 `AmethystUpdateScheduler`（参考 EHR 的 PerSecondUpdateScheduler）
        //   把它们**按槽位摊到不同帧**：每帧只做其中一两项，总频率不变，
        //   但帧时间变得平滑，不再有周期性尖峰。
        if (Time.unscaledTime >= _perfNext)
        {
            _perfNext = Time.unscaledTime + 0.1f;

            // ⚠ 帧率统计**不在这里** —— 它必须每帧采样，已提到 Update 开头（见上面的说明）。

            if (AmethystUpdateScheduler.ShouldRun(Slot.Notifier)) { try { AmethystNotifierLayer.Apply(); } catch { } }
            // 第 196 轮：模组水印不再走每帧轮询。
            // 旧实现在这里每帧写 `localScale` 并在 disabled 时重复调 `ShowModStamp()`，
            // 后者会反复重播入场动画 —— 这就是"右上角模组图标一直位移"的根因。
            //
            // 第 200 轮：模组图标改由 `ModManager.LateUpdate` 的 Postfix **每帧**驱动
            // （与 BetterAmongUs / FinalSuspect 一致，它们都靠每帧调用维持图标常显）。
            // 这里保留一次低频兜底，且**不再**套 `ShouldRun(Slot.ModStamp)` ——
            // 本块本身已是 0.1s 节流，再叠一个每帧轮转的 32 槽调度会形成两个不同周期，
            // 可能长期不命中（这就是此前"整局没有 [ModStamp] 日志"的原因之一）。
            try { Patches.ModStampState.Tick(); } catch { }

            // 技能 / 动作按钮缩放：内部只做一次浮点比较，值没变立即返回（几乎零开销），
            // 所以不套 `ShouldRun` 槽位 —— 菜单里拖滑块时要能立刻看到效果。
            try { Patches.AmethystAbilityButtonScale.Tick(); } catch { }

            if (AmethystUpdateScheduler.ShouldRun(Slot.ExileName)) { try { AmethystExileName.Tick(); } catch { } }
            if (AmethystUpdateScheduler.ShouldRun(Slot.MeetingIntro)) { try { AmethystMeetingIntroNameColor.Tick(); } catch { } }

            // 职业颜色：开场「职业分配阶段」着色的**兜底**路径。
            // 主路径是 ShowRole 协程状态机的 Postfix（见 AmethystShowRoleStateMachinePatch）；
            // ⚠ 这一项**不做**每秒限流：开场动画只有几秒，必须每帧跟进才能跟上淡入节奏。
            try { AmethystIntroRoleColor.Tick(); } catch { }

            if (AmethystUpdateScheduler.ShouldRun(Slot.Console)) { try { AmethystConsoleHider.Tick(); } catch { } }

            // 职业信息面板（第 171 轮新功能）：开关与可见性规则从配置读一次注入，
            // 面板本体不直接依赖配置类型。
            if (AmethystUpdateScheduler.ShouldRun(Slot.RolePanel))
            {
                try
                {
                    Amethyst.Patches.AmethystRolePanel.Enabled = AmethystConfig.RolePanel == null || AmethystConfig.RolePanel.Value;
                    Amethyst.Patches.AmethystRolePanel.ShowDeadRoles = AmethystConfig.ShowRolesWhenDead != null && AmethystConfig.ShowRolesWhenDead.Value;
                    Amethyst.Patches.AmethystRolePanel.ShowTeamRoles = AmethystConfig.ShowImpostorTeammateRole != null && AmethystConfig.ShowImpostorTeammateRole.Value;
                    Amethyst.Patches.AmethystRolePanel.Tick();
                }
                catch { }
            }
        }

        // （会议开场名字着色已并入上面的 0.1s 节流块）

        // 品牌替换（Among Us 标志 → 模组字标）与"小绿人/加载条变紫"：
        // 每帧调用，内部自己节流（热对象 0.05s、整个主界面 0.5s）——
        // 只在 0.5s 做的话，鼠标悬停按钮变色后要等半秒才被我们改紫。
        // 自适应性能保护：掉帧时自动关掉本模组的可选视觉开销
        // ⚠ 第 200 轮：**品牌替换（AmethystRebrand）已停用**。
        //   按需求"移除模组标识、直接使用游戏原版的标识"：
        //   不再隐藏 `LOGO-AU` / `LogoAnimator`，也不再放置模组字标（SplashIcon）。
        //   （原主菜单背景图走的是 AmethystMainMenuArt，与此无关，仍然保留。）
        // try { AmethystRebrand.Tick(); } catch { }
        // 主界面优化：低频（0.5s）应用/还原，保证中途开关也能立即生效。
        if (Time.unscaledTime >= _mainMenuNext)
        {
            _mainMenuNext = Time.unscaledTime + 0.5f;
            try { AmethystMainMenuOptimize.TickLive(); } catch { }
            try { Patches.AmethystMainMenuButtons.TickLive(); } catch { }
            try { Patches.AmethystUiTheme.TickLive(); } catch { }
        }

        // ===== 复盘信息 =====
        // 大厅与结算支持按钮和 F2 切换，离开可用场景时收起。
        try
        {
            if (!AmethystRecap.CanUse && AmethystRecapWindow.IsOpen)
                AmethystRecapWindow.Hide();
        }
        catch { }

        // 第 221 轮：复盘按钮改用游戏原生精灵按钮（大厅 + 结算各挂一个），这里驱动它建立 / 同步标签。
        try { AmethystRecapButton.Tick(); } catch { }

        try
        {
            if (AmethystConfig.RecapKey.Value != KeyCode.None && Input.GetKeyDown(AmethystConfig.RecapKey.Value))
            {
                if (AmethystRecap.CanUse)
                    AmethystRecapWindow.ToggleVisible();
            }
        }
        catch { }

        // 对局中低频快照玩家数据（只采集不显示，供回到大厅后查看）。
        // ⚠ 第 170 轮修复：这里原来复用 `_perfNext` 做节流，但紧邻上面的 0.1s 块
        //   刚刚把 `_perfNext` 设成 `now + 0.1`，于是本判断 `now >= _perfNext`
        //   **恒为 false** —— 快照实际从未执行，复盘数据在对局中根本没被采集。
        //   现在改用独立计时器；顺带把它从 0.1s 块里解耦，节流语义也不再依赖
        //   上面那个块是否真的跑过。
        if (Time.unscaledTime >= _recapSyncNext)
        {
            _recapSyncNext = Time.unscaledTime + 0.1f;
            try
            {
                AmongUsClient net = AmongUsClient.Instance;
                if (net != null && net.IsGameStarted && LobbyBehaviour.Instance == null)
                    AmethystRecap.Sync();
            }
            catch { }
        }

        // 结算界面出现后低频抓取「本局结果」与房号，并冻结复盘
        if (Time.unscaledTime >= _recapNext)
        {
            _recapNext = Time.unscaledTime + 0.5f;
            try
            {
                if (AmethystRecap.TryCaptureEndGame())
                {
                    AmethystRecap.Sync();

                }
            }
            catch { }
        }

        HudManager hud = HudManager.Instance;
        if (hud != null)
        {
            try { AmethystCooldownColorState.Tick(hud); } catch { }
            try { AmethystAlwaysChatState.Tick(hud); } catch { }
            // 会议中保持任务面板/职业信息面板可见（参考 AUnlocker）
            try { AmethystMeetingTaskPanel.Apply(hud); } catch { }
        }
    }

    public void LateUpdate()
    {
        // 搜索房间列表：已整体照抄 BetterAmongUs（见 Patches/LobbySearchPatches.cs），
        // 滚动完全由游戏自带的 `Scroller` 驱动，Amethyst 不再每帧干预列表位置。

        // 帧率：
        //   · 60/未解锁 -> 开启垂直同步（消除 60Hz 下的撕裂与帧节奏抖动）
        //   · 解锁且上限 > 60 -> 关闭垂直同步，真正跑到设定上限（否则"解锁没用"）
        // 若已装独立的「FPS & Network Optimizer」，帧率策略交给它，我们不再干预。
        if (AmethystCompat.OtherFps) return;
        int cap = 60;
        try { if (AmethystConfig.FpsLimit != null) cap = Mathf.Clamp(AmethystConfig.FpsLimit.Value, 60, 240); } catch { }
        bool highCap = AmethystConfig.UnlockFps240.Value && cap > 60;
        int desiredVSync = highCap ? 0 : 1;
        int desiredTarget = highCap ? cap : -1;
        if (QualitySettings.vSyncCount != desiredVSync || Application.targetFrameRate != desiredTarget)
        {
            QualitySettings.vSyncCount = desiredVSync;
            Application.targetFrameRate = desiredTarget;
        }
    }

    private static void CopyLobbyCode()
    {
        Menu.AmethystLobbyCopyHelper.CopyRoomInfo();
    }
}
