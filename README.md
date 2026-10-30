<div align="center">

# Amethyst v1.1.7

<img width="1602" height="898" alt="Amethyst" src="https://github.com/user-attachments/assets/1d7ba60f-5f79-494d-a9ab-a22461f07588" />

**Among Us 反作弊与实用功能模组**

<a href="#简体中文">简体中文</a> · <a href="#english">English</a> · <a href="#русский">Русский</a>

<br>

<img src="https://img.shields.io/badge/Among%20Us-IL2CPP-000000?style=for-the-badge&logo=amongus&logoColor=white" alt="Among Us">
<img src="https://img.shields.io/badge/BepInEx-6.x-5865F2?style=for-the-badge" alt="BepInEx">
<img src="https://img.shields.io/badge/.NET-6.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET">
<img src="https://img.shields.io/badge/version-1.1.7-9b59b6?style=for-the-badge" alt="version">
<img src="https://img.shields.io/badge/License-GPL--3.0-blue?style=for-the-badge&logo=gnu&logoColor=white" alt="License">

</div>

---

# 简体中文

## 目录

- [简介](#简介)
- [功能](#功能)
  - [反作弊](#-反作弊)
  - [管道 / 滑索保护](#-管道--滑索保护)
  - [玩家管理](#-玩家管理)
  - [网络保护](#-网络保护)
  - [捉迷藏保护](#-捉迷藏保护)
  - [房间与会话](#-房间与会话)
  - [对局回顾](#-对局回顾)
  - [音乐播放器](#-音乐播放器)
  - [胜利者角色显示](#-胜利者角色显示)
  - [角色颜色](#-角色颜色)
  - [角色信息面板](#-角色信息面板)
  - [界面优化](#-界面优化)
  - [性能](#-性能)
  - [启动与更新检查](#-启动与更新检查)
  - [模组客户端检测](#-模组客户端检测)
  - [个性化](#-个性化)
- [快捷键](#快捷键)
- [安装](#安装)
- [反作弊提示](#反作弊提示)
- [从源码构建](#从源码构建)
- [参考代码与致谢](#参考代码与致谢)
- [免责声明](#免责声明)
- [许可证](#许可证)

---

## 简介

Amethyst（紫晶）是面向私人房间的 Among Us 反作弊模组。它会识别“不可能的 RPC”（船员击杀、船员破坏、会议中钻管道、替别人钻管道、管道踢人漏洞等等），判断玩家是否作弊，并让房主选择处理方式：**无 / 警告 / 踢出 / 封禁**。模组同时包含进出房检测、封禁名单、等级守卫，以及大量对局体验功能。

> 本模组供房主在自己的私人房间使用。请勿在公开房间中使用，否则会破坏其他玩家的游戏体验。

---

## 功能

### 🛡 反作弊

- **RPC 检测** —— 拦截不可能的 RPC：船员击杀、船员破坏、无权限钻管道、伪造管道 ID、会议中钻管道 / 破坏 / 关门、地图外破坏、未授权的变形 / 隐身，以及其他角色越权行为
- **报告 / 投票校验** —— 拒绝会议中的报告、报告自己或存活玩家、在死亡状态或处于管道 / 梯子 / 平台 / 变形过程中发起的报告，以及会议之外的投票
- **房主专属 RPC** —— 作为房主丢弃伪造的 `SetRole` / `SetInfected` / `SetTasks` / `VotingComplete` / `SyncSettings` 包；这些包只可能由房主发出，收到即说明有客户端在伪造
- **破坏修复校验** —— 只有玩家确实站在控制台附近时才接受修灯 / 氧气 / 反应堆 / 通讯（房主侧，仅提示）
- **关门限制** —— 会议与放逐期间丢弃关门请求；0.01 秒内的第二次关门会被拒绝（一键全关作弊）
- **击杀检测** —— 内鬼击杀内鬼、已死亡玩家发起击杀、反复击杀同一个已死亡玩家
- **丢弃 RPC** —— 作为房主直接丢弃被标记的 RPC 包，使其不生效
- **处理方式** —— 可选 **无 / 警告 / 踢出 / 封禁**，另有 **错误踢出** 一档，走游戏自带的 `ClientTimeout` 拒绝路径
- **RPC 频率检测** —— 检测单个客户端高频发送 RPC（阈值 25 次/秒，仅提示）
- **开局会议拦截** —— 开局前 15 秒内阻止会议与报告
- **大厅假会议拦截** —— 拦截大厅中伪造的会议界面
- **无内鬼结算** —— 若内鬼全部离开或死亡，由房主结束本局，而不是让游戏卡在黑屏的“没有内鬼”界面

> 未知的 RPC ID 一律放行。游戏更新与角色模组会不断引入新的 ID，拦截它们会造成误判。

### 🚪 管道 / 滑索保护

- **管道规则** —— 会议中钻管道、无权限钻管道、伪造管道 ID、替别人钻管道
- **反强制钻管 / 反强制弹出** —— 拦截针对你客户端的强制穿越与强制弹出
- **管道踢人漏洞保护** —— 作为房主，惩罚发送该漏洞包的客户端
- **反强制滑索** —— 拦截针对你客户端的强制滑索

### 🕵 玩家管理

- **进出房检测** —— 显示加入玩家的昵称、平台与等级
- **封禁名单** —— 按 FriendCode / PUID 在加入时踢出 / 封禁
- **等级守卫** —— 设置最低 / 最高等级并选择处理方式
- **玩家历史 / 作弊历史**（**始终开启**，只写入文件，任何界面都不显示）：
  - 每次进出房都把昵称 / FriendCode / PUID / 平台 / 等级写入 `Amethyst/PlayerHistory.txt`
  - 每次反作弊命中都把昵称 / FriendCode / PUID / 平台 / 原因写入 `Amethyst/CheatHistory.txt`（与封禁名单同一目录）
  - 支持**按 FriendCode 回查历史**，用于找回突然退出玩家的昵称

### 🌊 网络保护

- 丢弃超大的 GameData 包，防止大包导致的崩溃 / 过载
- 丢弃类型非法或损坏的 GameData 子消息
- 对 VotingComplete 的投票数组做尺寸校验，防止分配过载
- **刷怪保护** —— 把单帧内的大量生成裁剪掉，防止卡死 / 崩溃

### 🧩 捉迷藏保护

- 检测并拦截捉迷藏模式下的异常报告、关门、破坏与非法钻管道

### 👥 房间与会话

- **复制房间信息**（`F6`）—— 把当前房间信息复制到剪贴板。**文字**跟随模组语言，**数值**始终来自房间本身：

  ```
  ABCD(7/10)
  模式:经典模式
  语言:英语
  房主:SomeName
  服务器:亚洲
  ```

  `模式` 是房间当前的游戏模式（经典模式 / 捉迷藏，含“愚人”变体），`语言` 是**房间**的语言设置 —— 所以如果房间是英文，而你的模组是中文，就会显示 `语言:英语`；模组是俄语则显示 `语言:Английский`。
- **会议中显示房主** —— 会议期间在左上角显示房主
- **角色显示** —— 你死亡后显示所有人的角色；存活时可以选择显示自己的角色以及内鬼队友的角色
- **对局中踢出 / 封禁** —— 房主可以在对局进行中踢出 / 封禁
- **抢色** —— 仅限大厅，目标颜色空出时自动抢到（可选，默认关闭）
- **自动返回大厅** 与 **NoWin**（永不结束对局）
- **开发者页签**（`Developer`）—— **性能探针** 与 **无胜利条件**，与常规设置分开放置

### 📋 对局回顾

一个**从左侧滑入的回顾面板**（固定在左侧，不可拖动），显示**上一局**每位玩家的：角色（存活 `=>` 死亡）、击杀数 / 任务进度、死因（被击杀并标出凶手 / 被放逐 / 掉线 / 死亡 / 存活）以及本局结果。

- 一键复制为纯文本
- 屏幕左上角有 **显示 / 隐藏回顾信息** 按钮，`F2` 快捷键效果相同
- 大厅与结算界面都能用（不再自动弹出，默认隐藏）
- 对局中存活期间保持隐藏 —— 中途显示别人角色等同于作弊

### 🎵 音乐播放器

内置音乐播放器（`M`），在游戏时播放**你自己的音频文件**。

**把曲目放到：**

```
<Among Us 安装目录>\Amethyst\Music\
```

就是存放封禁名单、`PlayerHistory.txt` 与 `CheatHistory.txt` 的那个 `Amethyst` 文件夹。首次使用时自动创建，路径始终显示在**播放器窗口内**，不需要猜。

| 格式 | 扩展名 |
|:-------|:-----------|
| MP3 | `.mp3` |
| WAV | `.wav` |
| Ogg Vorbis | `.ogg` |

**功能**

- **播放列表** —— 列出文件夹内所有曲目，按名称排序；点击任意一行即可播放
- **播放控制** —— 上一首 / 播放暂停 / 下一首 / 停止，另有**循环**按钮在 **单曲 → 列表 → 播放一次** 之间切换
- **进度条** —— 主题风格的进度条与时间显示（点击或拖动后**松开**才跳转；只在松开时重新定位音频，所以拖动不会卡顿）
- **音量滑块**，配 − / + 微调按钮，风格与模组菜单一致（0–100%）
- **刷新** —— 重新扫描文件夹，**游戏运行期间**放进去的文件也能立刻出现
- **记忆**窗口位置（拖动标题栏）、音量与上次的循环模式
- **自动续播** —— 播完自动下一首；循环模式为**播放一次**时，最后一首播完即停止
- **正在播放行**始终显示 **`正在播放 <曲名>`**（或 `已暂停 <曲名>`），一眼就能看到当前曲目
- 播放时**静音游戏自带音乐** —— 大厅音乐与主菜单音乐都会被静音，避免与你的曲目互相干扰；暂停、停止或音量归零后立即恢复。独立开关：**设置 → 大厅与房间列表 → 播放时静音游戏音乐**（`Music.MuteGameMusic`，默认开启）。环境音效**不会**被处理
- 解码在后台线程进行，播放较大的曲目不会卡住游戏
- 超过 **20 分钟** 的曲目会被拒绝（解码后的音频常驻内存，用此上限约束）

**说明**

- 播放器**完全本地、客户端侧**：只有你能听到，不会经过网络
- 音频由两个内置的第三方开源库解码（**NAudio** 处理 MP3/WAV，**NVorbis** 处理 OGG，均为 MIT）。它们嵌在 `Amethyst.dll` 内，首次使用时释放到 `Amethyst\libs\`，**不需要额外下载**。许可证文本放在同一目录 —— 见 `THIRD-PARTY-NOTICES.txt`
- 曲目加载失败时窗口显示 `加载失败`；到 `BepInEx\LogOutput.log` 里找带 `[Music]` 的行即可看到原因

### 🏆 胜利者角色显示

- 结算界面上，**获胜玩家的角色**显示在结果头像的**名字下方**
- 获胜名单取自游戏自身缓存的获胜者，角色取自模组在局内的快照，因此跨结算场景切换依然有效
- 可在 **其他功能 → 界面优化** 中单独开关

### 🎮 角色颜色

每个**原版角色**都用各自的颜色渲染，而不是统一的内鬼红 / 船员蓝。

| 角色 | 颜色 |
|:-----|:------|
| 船员 | `#8CFFFF` |
| 内鬼 | `#C61111` |
| 工程师 | `#FF6A00` |
| 科学家 | `#8EE98E` |
| 守护天使 | `#77E6D1` |
| 追踪者 | `#34AD50` |
| 鸣笛者 | `#FF4A62` |
| 侦探 | `#625EEE` |
| 灵魂向导（影响者） | `#FF0066` |
| 变形者 | `#C61111` |
| 幻影 | `#C61111` |
| 毒蛇 | `#C61111` |
| 内鬼幽灵 | `#C61111` |
| 船员幽灵 | `#8CFFFF` |
| 审判者 | `#F8D85A` |

- 应用于**角色分配界面**（“你是……”介绍文字）、**头顶角色名**、会议、聊天与角色信息面板
- 未来游戏更新新增的角色会自动回退到其阵营颜色，启动自检会报告没有专属颜色的角色

### 📖 角色信息面板

一个游戏内面板（类似任务面板，可点击折叠 / 展开），显示你的角色与说明。

- 15 个原版角色各有一句**风味台词**与一段**详细规则文本**，**完整支持中文、英文、俄语**
- 幽灵角色判定使用游戏自带的 `RoleManager.IsGhostRole`，未来新增的角色也能自动识别
- 存活且为船员时只显示自己的角色；死亡后（或身为内鬼时）还会列出你有权看到的其他玩家

### 🎨 界面优化

> 主菜单右栏的滑入动画是内置的，**始终开启**。

本组功能每一项都有**独立开关**，在 **其他功能 → 界面优化** 中单独设置：

- **主菜单精简** —— 去掉杂乱元素（背景噪点、左栏填充与分隔线、窗口高亮、全屏色罩、好友请求角标），同时**保留 Among Us 标志**
- **主菜单滑入** —— 点击“开始 / 我的账户 / 制作人员”时右栏从右侧滑入；打开设置、公告、库存或商店时滑出，不再遮住弹窗
- **主菜单链接按钮** —— 主菜单左下角三个按钮（**GitHub**、**QQ 群**、**举报作弊者**），同一模板生成，外观完全一致。**举报作弊者**会在浏览器中打开举报页 **`api2.elauk.top/acban/submit`**，同时把链接复制到剪贴板作为兜底并弹出提示。三者由 `Other.MainMenuButtons` 开关统一控制
- **会议角色标记** —— 会议中每位**存活**玩家头像旁有一个 `EditTag` 图标（死亡玩家没有图标，已有的标记也会隐藏）。点击后打开游戏自带的变形者菜单，给该玩家标注角色；标记显示在**会议名字旁**以及**游戏内该玩家头顶**。内鬼以红色排在前面，船员随后以蓝色列出，末尾有“清除标记”项。对局结束时所有标记自动清空（仅本地生效 —— 只有你能看到；你自己那一格没有图标）
- **对局回顾** —— 在大厅按 `F2` 查看上一局（见上文[对局回顾](#-对局回顾)）
- **会议中的任务面板** —— 会议期间保持任务列表可见，并置于会议界面之上
- 强制显示开始按钮、跳过击杀动画
- **更好的延迟显示** —— 游戏内读数，**可逐项选择**：延迟、FPS、服务器 / 区域、**房间号 + (当前人数/上限)** 以及**房主名字**。在 **其他功能 → 冷却与延迟显示** 中逐项开关；未勾选的项直接不显示
- **服务器显示** —— 显示你所在的区域 / 服务器，按语言本地化（例如 "Asia" /「亚洲」/«Азия»），不使用缩写
- **房间信息** —— 找游戏列表最多显示 10 个房间，可滚动（**整行滚动**，行永远不会被画到列表区域外），每行显示房主名字 / 平台 / 房间号
- **颜色名称显示** —— 在玩家附近显示其颜色名称
- **名字颜色** —— 自己与其他玩家的名字用其颜色渲染；以内鬼视角看，队友显示为红色（作用于聊天气泡、会议投票与“谁死了 / 谁报告的”介绍界面；放逐结果界面保持游戏原样）
- 显示选项：会议左上角显示房主、显示自己的角色、显示内鬼队友角色、死亡后显示所有人角色
- **外观保存** —— 库存页 6 个预设按钮，一键切换外观
- **解锁 240 FPS**、**解锁全部外观**、**跳过掉线惩罚** 与 **自动返回大厅** 各自有**独立开关**（在“显示与角色”下），不跟随任何其它开关
- **更好的聊天** —— 富文本输入、复制粘贴、气泡动画、暗色主题
- 悬浮菜单按钮

### ⚡ 性能

全部位于 **其他功能 → 性能**，**默认开启**（不想要可以关掉）：

- **不更新死亡玩家** —— 对**已死亡**玩家跳过大部分 `FixedUpdate` 帧。满员房间里这是收益最大的一项。跳帧数滑块（2–300，默认 60）控制力度。代价是死亡玩家的覆盖层刷新会稍慢
- **低负载模式** —— 玩家轮流更新：每帧只完整更新一名玩家，其余跳过。仅在超过 8 人时启用；本地玩家与房主永远不会被跳过
- **隐藏控制台窗口** —— 启动时隐藏 BepInEx 的黑色控制台窗口
- **每秒调度器**把模组自身的周期任务分散到多帧执行，而不是挤在一帧里，从而消除周期性的帧时间尖峰

### 🚀 启动与更新检查

- **启动时检查更新** —— 模组在后台检查新版本，有更新时在模组菜单里显示提示。检查过程不会阻塞或拖慢游戏正常启动
- **检查更新按钮** —— 主菜单上的按钮，位于 GitHub / QQ 群 / 举报作弊者旁边。它读取 GitHub 最新发布、与当前版本比较，做任何操作前都会先询问
- **应用内下载与替换** —— 确认后下载新 DLL、校验并就地替换，重启游戏即完成。只有校验和、大小与程序集名称全部通过的下载才会替换，替换失败会回滚到原文件

### 🔍 模组客户端检测

- 检测并标记房间内其他兼容的模组客户端
- 在大厅里于其头顶显示检测到的**Amethyst 版本** —— 格式为 `♥ Ame{版本} <玩家> ♥`（例如中文下 `♥ Ame1.1.7 用户 ♥`，英文下 `♥ Ame1.1.7 User ♥`）。你自己的标记也会显示版本
- 版本号随识别兼容客户端的那次握手一起传输，因此不需要额外流量。未声明版本的构建仍会显示朴素的 `♥ Ame 用户 ♥` 标记

### 🌐 个性化

- 语言：中文 / English / Русский
- 可调界面主题与缩放；可关闭遥测 / 崩溃上报

---

## 快捷键

| 按键 | 作用 |
|:---:|:-------|
| `Insert` | 打开 / 关闭主菜单 |
| `F2` | 显示 / 隐藏对局回顾（大厅与结算界面） |
| `F6` | 复制当前房间信息 |
| `M` | 打开 / 关闭音乐播放器 |

**音乐播放器窗口打开时**，以下按键可用（均可在 **设置 → 快捷键** 中修改）：

| 按键 | 作用 |
|:---:|:-------|
| `[` / `MusicPrevKey` | 上一首 |
| `]` / `MusicNextKey` | 下一首 |
| `P` / `MusicPlayPauseKey` | 播放 / 暂停 |

> 快捷键可在模组菜单的 **设置 → 快捷键** 中修改（点击按键标签，按下新键；`Esc` 取消，右键或垃圾桶图标清除），也可以改 `BepInEx/config/amethyst.mod.cfg`（`Keys.MenuKey` / `Keys.RecapKey` / `Keys.CopyCodeKey` / `Keys.MusicKey` / `Keys.MusicPrevKey` / `Keys.MusicNextKey` / `Keys.MusicPlayPauseKey`）。

---

## 安装

> Amethyst 需要 **BepInEx（IL2CPP，Windows x64）**。

1. 下载并安装 **BepInEx BleedingEdge — IL2CPP（`win-x64`）**
2. 找到 Among Us 的安装目录：
   - **Steam**：库 → 右键 Among Us → 管理 → 浏览本地文件
   - **Epic**：库 → Among Us → 管理，通常在 `C:\Program Files\Epic Games\AmongUs`
3. 把 BepInEx 解压到游戏目录（与 `Among Us.exe` 同级）
4. 启动游戏到主菜单后关闭（这会生成 `BepInEx/plugins` 文件夹）
5. 把 **`Amethyst_v1.1.7.dll`** 放进 `BepInEx/plugins/`
6. 启动游戏，按 `Insert` 打开菜单

> 首次启动会在 `BepInEx/config/` 下生成配置文件；删除对应的 `.cfg` 即可重置设置。

---

## 反作弊提示

一次反作弊命中会弹出通知并按配置执行处理（警告 / 踢出 / 封禁）。常见检测包括但不限于：

| 类别 | 说明 |
|:---------|:------------|
| 船员击杀 | 船员角色发起击杀 |
| 内鬼击杀内鬼 | 内鬼击杀队友 |
| 死亡玩家击杀 | 已死亡玩家发起击杀 |
| 反复击杀已死亡玩家 | 反复击杀同一个已死亡目标 |
| 船员破坏 / 地图外破坏 | 船员发起破坏，或从非法位置发起破坏 |
| 快速破坏 | 短时间内破坏不同系统 |
| 会议中钻管道 / 破坏 / 关门 | 会议期间不可能出现的行为 |
| 无权限钻管道 / 伪造管道 ID | 没有对应权限，或使用伪造的管道 ID |
| 替别人钻管道 | 强制让其他玩家进入 / 离开管道 |
| 管道踢人漏洞 | 利用管道踢人漏洞 |
| 强制滑索 | 强制使用滑索 |
| 滥用报告 / 投票 | 会议中报告、报告自己或存活玩家、会议外投票 |
| 伪造房主 RPC | 客户端发送 `SetRole` / `SetInfected` / `SetTasks` / `VotingComplete` / `SyncSettings` |
| 远程修复 | 未站在控制台附近却修复破坏 |
| 关门刷屏 | 0.01 秒内关闭多个门，或会议中关门 |
| 捉迷藏报告 / 破坏 / 关门 / 非法钻管 | 捉迷藏模式下的异常行为 |
| 大厅对局 RPC | 对局开始前发送假的对局 RPC（击杀、会议、变形） |
| RPC 频率异常 | 单个客户端高频发送 RPC |

---

## 从源码构建

你需要 Among Us 的程序集引用（`Assembly-CSharp.dll` 等）。项目通过 `GameRefsDir` 指向解包出来的引用目录。

```
dotnet build src/Amethyst.csproj -c Release
```

构建产物为 `src/bin/Release/netcoreapp6.0/Amethyst.dll`（另有一份带版本的副本 `Amethyst_v1.1.7.dll`）。

---

## 参考代码与致谢

Amethyst 的反作弊规则、RPC 处理与界面行为在编写时参考了以下开源 Among Us 项目。感谢这些作者，也请支持他们：

- **[BetterAmongUs](https://github.com/D1GQ/BetterAmongUs)** —— RPC 发送 / 网络对象解析，以及整体反作弊结构
- **[OnyxMenu](https://github.com/Veltrix-s/OnyxMenu)** —— RPC 守卫、传送与 AFK 守卫、菜单动画与布局
- **[Hydra](https://github.com/MrDiamond64/Hydra)** —— 逐条 RPC 校验、`SendLateRejection` 踢出路径、保护模块的组织方式
- **[EndlessHostRoles](https://github.com/Gurge44/EndlessHostRoles)** —— 角色颜色、房主工具与界面补丁
- **[SickoMenu](https://github.com/g0aty/SickoMenu)** —— SMAC 反作弊规则集，用作规则覆盖面的参考
- **[BanMod](https://github.com/GiannBart/BanMod)** —— 漏洞守卫、会议黑屏守卫与反作弊模块

各项目版权归其作者所有，并按各自许可证分发。Amethyst 不打包、也不再分发它们的二进制文件。

---

## 免责声明

Amethyst 是 Among Us 的非官方第三方修改。

本模组与 Among Us 或 Innersloth LLC 无关，其中的内容未获 Innersloth LLC 认可或赞助。本文材料的部分内容归 Innersloth LLC 所有。© Innersloth LLC。

Amethyst 仅限在私人房间使用。

本软件按“现状”提供，不附带任何明示或暗示的担保。安装或使用即表示你接受随之而来的一切后果，包括但不限于账号限制、封禁、踢出、崩溃、进度丢失、文件损坏、游戏不稳定，或与未来游戏更新不兼容。开发者不对使用本软件产生的任何后果、损害或滥用承担责任。

---

## 许可证

本项目基于 **GNU GPL v3.0** 发布（见 [LICENSE](LICENSE)）。你可以自由使用、研究、分享和修改，但衍生作品必须采用相同的许可证。

---
# English

## Table of Contents

- [Overview](#overview)
- [Features](#features)
  - [Anti-Cheat](#-anti-cheat)
  - [Vent / Zipline Protection](#-vent--zipline-protection)
  - [Player Management](#-player-management)
  - [Network Protection](#-network-protection)
  - [Hide & Seek Protection](#-hide--seek-protection)
  - [Room & Session](#-room--session)
  - [Recap Info](#-recap-info)
  - [Music Player](#-music-player)
  - [Winner Role Reveal](#-winner-role-reveal)
  - [Role Colors](#-role-colors)
  - [Role Info Panel](#-role-info-panel)
  - [UI Optimization](#-ui-optimization)
  - [Performance](#-performance)
  - [Startup & Update Check](#-startup--update-check)
  - [Mod Client Detection](#-mod-client-detection)
  - [Personalization](#-personalization)
- [Hotkeys](#hotkeys)
- [Installation](#installation)
- [Anti-Cheat Notices](#anti-cheat-notices)
- [Building From Source](#building-from-source)
- [Reference Code & Credits](#reference-code--credits)
- [Disclaimer](#disclaimer)
- [License](#license)

---

## Overview

Amethyst (紫晶) is an Among Us anti-cheat mod for private lobbies. It detects "impossible RPCs" (crewmate kills, crewmate sabotage, venting during a meeting, venting on another player's behalf, the vent-kick exploit, and so on), decides whether a player is cheating, and lets the host choose an action: **None / Warn / Kick / Ban**. It also bundles join/leave detection, a ban list, a level guard, and many match quality-of-life features.

> This mod is intended for hosts to use in their own private lobbies. Please do not use it in public lobbies, as it would ruin the experience for others.

---

## Features

### 🛡 Anti-Cheat

- **RPC detection** — catches impossible RPCs: crewmate kills, crewmate sabotage, venting without permission, forged vent IDs, venting / sabotage / door-closing during meetings, sabotage outside the map, unauthorized shapeshift / vanish, and other role violations
- **Report / vote validation** — rejects reports during a meeting, reports of yourself or of a living player, reports while dead or while inside a vent / on a ladder / platform / mid-shapeshift, and votes cast outside a meeting
- **Host-authority RPCs** — as host, drops forged `SetRole` / `SetInfected` / `SetTasks` / `VotingComplete` / `SyncSettings` packets; these are only ever sent by the host, so receiving one means a client spoofed it
- **Sabotage repair validation** — repairing lights / oxygen / reactor / comms is only accepted when the player is actually standing near the console (host-side, notify-only)
- **Door-close limits** — door closing is dropped during meetings and exile, and a second close within 0.01 s is rejected (one-key close-all cheat)
- **Kill detection** — impostor killing an impostor, a dead player initiating a kill, repeatedly killing an already-dead player
- **RPC dropping** — as host, drops the flagged RPC packets so they never take effect
- **Actions** — choose **None / Warn / Kick / Ban**, plus an **Error kick** tier that uses the game's own `ClientTimeout` rejection path
- **RPC flood detection** — detects a single client spamming RPCs (threshold 25/s, notify-only)
- **Early meeting blocking** — blocks meetings / reports during the first 15 seconds
- **Fake lobby meeting blocking** — blocks spoofed meeting UI in the lobby
- **No-impostor settlement** — if every impostor has left or died, the host ends the round instead of leaving the game on the black "no impostors" screen

> Unknown RPC IDs are always allowed. Game updates and role mods introduce new IDs, and blocking them caused false positives.

### 🚪 Vent / Zipline Protection

- **Vent rules** — venting during a meeting, venting without permission, forged vent IDs, venting on another player's behalf
- **Anti force-vent / anti vent-eject** — blocks forced passthrough and ejection aimed at your client
- **Vent-kick exploit protection** — as host, punishes the client that sends the exploit packet
- **Anti forced zipline** — blocks forced zipline rides aimed at your client

### 🕵 Player Management

- **Join/leave detection** — shows a joining player's nickname, platform and level
- **Ban list** — kick / ban on join by FriendCode / PUID
- **Level guard** — set a minimum / maximum level and choose an action
- **Player history / cheat history** (**always on**, written to file only and never shown in any UI):
  - every join/leave records name / friend code / PUID / platform / level to `Amethyst/PlayerHistory.txt`
  - every anti-cheat hit records name / friend code / PUID / platform / reason to `Amethyst/CheatHistory.txt` (same folder as the ban list)
  - supports **reading history back** by friend code, which is used to recover the names of players who left abruptly

### 🌊 Network Protection

- Drops oversized GameData packets to prevent large-message crashes / overload
- Drops GameData sub-messages with an invalid / malformed type
- Guards VotingComplete against an oversized voter array (allocation overload)
- **Spawn flood protection** — trims a flood of spawns in a single frame to prevent freezes / crashes

### 🧩 Hide & Seek Protection

- Detects and blocks abnormal reports, door closes, sabotage and illegal venting in H&S

### 👥 Room & Session

- **Copy room info** (`F6`) — copies the current room to your clipboard. The wording follows the **mod's language**, while the **values** always come from the **room itself**:

  ```
  ABCD(7/10)
  Mode:Classic
  Language:English
  Host:SomeName
  Server:Asia
  ```

  `Mode` is the room's current game mode (Classic / Hide & Seek, including the Fools variants) and `Language` is the **room's** language setting — so an English room reads `Language:英语` if your mod is set to Chinese, and `Language:Английский` if it is set to Russian.
- **Show host in meetings** — shows the host in the top-left corner during a meeting
- **Role display** — shows everyone's role after you die; while alive you can show your own role and your impostor teammates' roles
- **Kick/ban during a match** — the host can kick / ban mid-match
- **Color snipe** — lobby only, automatically claims your desired color when it becomes free (optional, off by default)
- **Auto return to lobby** and **NoWin** (never end the match)
- **Developer tab** (`Developer`) — **performance probe** and **no-win-conditions**, kept out of the way of the normal settings

### 📋 Recap Info

A **sliding-in recap panel** (docked on the left, not draggable) showing, for every player in the **previous match**: role (alive `=>` dead), kill count / task progress, cause of death (killed, with the killer listed / ejected / disconnected / died / survived) and the match result.

- One-click copy as plain text
- A **Show/Hide recap info** button sits in the top-left corner of the screen; the `F2` hotkey does the same
- Available both in the lobby and on the post-match screen (it no longer opens automatically; it starts hidden)
- While alive during a match it stays hidden — showing other players' roles mid-match would be cheating

### 🎵 Music Player

A built-in music player (`M`) that plays **your own audio files** while you play.

**Put your tracks in:**

```
<Among Us folder>\Amethyst\Music\
```

This is the same `Amethyst` folder that holds the ban list, `PlayerHistory.txt` and `CheatHistory.txt`. It is created automatically on first use, and its path is shown **inside the player window** at all times, so you never have to guess.

| Format | Extensions |
|:-------|:-----------|
| MP3 | `.mp3` |
| WAV | `.wav` |
| Ogg Vorbis | `.ogg` |

**Features**

- **Playlist** — every track in the folder, sorted by name; click any row to play it
- **Transport** — previous / play-pause / next / stop, plus a **repeat** button that cycles **single-track → playlist → play-once**
- **Progress bar** — a themed seek bar with a time readout (click, or drag and **release**, to jump; the audio is only re-positioned on release, so dragging never stutters)
- **Volume slider** with − / + fine-step buttons, styled like the rest of the mod's menu (0–100%)
- **Refresh** — re-scans the folder, so files dropped in **while the game is running** appear immediately
- **Remembers** both the window position (drag the title bar), the volume and the last repeat mode between launches
- **Auto-advance** — plays the next track on finish; when repeat is **play-once**, the player stops after the last track
- **Now playing line** always reads **`Now playing <track name>`** (or `Paused <track name>`), so you can see what is playing at a glance
- **Mutes the game's own music** while the player is playing — both lobby music and main-menu music are silenced so they do not fight with your track, and restored as soon as you pause, stop, or turn the volume to zero. Independent toggle: **Settings → Lobby & room list → Mute game music while playing** (`Music.MuteGameMusic`, on by default). Ambient sound effects are deliberately **not** touched
- Decoding runs on a background thread, so starting a large track does not freeze the game
- Tracks longer than **20 minutes** are rejected (decoded audio is held in memory; this bounds it)

**Notes**

- The player is **local and client-side**: only you hear the music, and nothing is sent over the network
- Audio is decoded with two bundled third-party open-source libraries (**NAudio** for MP3/WAV and **NVorbis** for OGG, both MIT). They are embedded inside `Amethyst.dll` and extracted to `Amethyst\libs\` on first use, so **no extra downloads are needed**. Their license texts are placed in that same folder — see `THIRD-PARTY-NOTICES.txt`
- If a track fails to load, the window shows `Load failed`; check `BepInEx\LogOutput.log` for a `[Music]` line with the reason

### 🏆 Winner Role Reveal

- On the post-match screen, the **winning players' roles** are shown **under their names** on the result avatars
- The winning lineup comes from the game's own cached winners, and roles are recovered from the mod's in-match snapshot, so it works across the end-screen scene change
- Toggleable independently under **Other Features → UI Optimization**

### 🎮 Role Colors

Every **vanilla role** is rendered in its own color instead of the flat impostor-red / crewmate-blue.

| Role | Color |
|:-----|:------|
| Crewmate | `#8CFFFF` |
| Impostor | `#C61111` |
| Engineer | `#FF6A00` |
| Scientist | `#8EE98E` |
| Guardian Angel | `#77E6D1` |
| Tracker | `#34AD50` |
| Noisemaker | `#FF4A62` |
| Detective | `#625EEE` |
| Spirit Guide (Influencer) | `#FF0066` |
| Shapeshifter | `#C61111` |
| Phantom | `#C61111` |
| Viper | `#C61111` |
| Impostor Ghost | `#C61111` |
| Crewmate Ghost | `#8CFFFF` |
| Judge | `#F8D85A` |

- Applied to the **role-assignment screen** (the intro "You Are" text), the **overhead role name**, meetings, chat, and the role-info panel
- Any role added by a future game update automatically falls back to its team color, and a startup self-check reports any role that has no dedicated color

### 📖 Role Info Panel

An in-game panel (a second task-like panel you can click to collapse/expand) showing your role and its description.

- Each of the 15 vanilla roles has a **short flavour line** and a **detailed rules text**, **fully localized in Chinese, English and Russian**
- Ghost-role detection uses the game's own `RoleManager.IsGhostRole`, so roles added by future game updates are picked up automatically
- While alive as a crewmate it only shows your own role; after you die (or as an impostor) it also lists the other players you are allowed to see

### 🎨 UI Optimization

> The main menu's right-panel slide-in animation is built in and **always on**.

Every feature in this group has its **own independent toggle**, set individually under **Other Features → UI Optimization**:

- **Main menu optimization** — trims the clutter (background noise, left panel fill and dividers, window highlights, full-screen tint, friend-request badge) while **keeping the Among Us logo**
- **Main menu slide-in** — clicking "Play / My Account / Credits" slides the right panel in from the right; opening Settings, Announcements, Inventory or Shop slides it out so it no longer covers the popup
- **Main menu link buttons** — three buttons at the bottom-left of the main menu (**GitHub**, **QQ Group** and **Report Cheater**), all built from the same template so they look identical. **Report Cheater** opens the cheat-report page **`api2.elauk.top/acban/submit`** in your browser, copies the link to the clipboard as a fallback and shows a toast. All three are hidden together with the `Other.MainMenuButtons` toggle
- **Meeting role tag** — in a meeting each **living** player has an `EditTag` icon next to their avatar (dead players get no icon, and any mark they had is hidden). Click it to open the game's own shapeshifter menu and mark that player with a role; the mark appears **next to the meeting name** and **above the player's head in game**. Impostors are listed first in red, crewmates after them in blue, with a "clear tag" entry at the end. All tags are cleared automatically when the match ends (local only — only you can see them; your own slot shows no icon)
- **Recap info** — press `F2` in the lobby to review the previous match (see [Recap Info](#-recap-info) above)
- **Task panel in meetings** — keeps your task list visible during meetings, raised above the meeting UI
- Force-show the start button, skip the kill animation
- **Better ping display** — an in-game readout with **selectable items**: latency, FPS, server/region, **room code + (players/max players)** and **host name**. Turn each one on or off under **Other Features → Cooldown & Ping Display**; unselected items are simply not shown
- **Server display** — shows which region/server you are on, localized per language (e.g. "Asia" / 「亚洲」 / «Азия»), with no abbreviations
- **Room info** — Find Game lists up to 10 lobbies in a scrollable list (scrolls **one whole row at a time**, so rows can never be drawn outside the list area), each row showing host name / platform / room code
- **Color name display** — shows each player's color name near them
- **Name colors** — your own and other players' names are rendered in their color; from an impostor's perspective teammates are shown in red (applies to chat bubbles, meeting votes and the "who died / who reported" intro screen; the ejection result screen keeps the game's native look)
- Display options: show the host in the top-left of a meeting, show your own role, show impostor teammates' roles, show everyone's role after you die
- **Outfit saving** — 6 preset buttons on the inventory page for one-click outfit switching
- **Unlock 240 FPS**, **unlock all cosmetics**, **skip the disconnect penalty** and **auto-return to lobby** each have their **own independent toggle** under "Display & Roles" — they do not follow any other switch
- **Better chat** — rich text input, copy/paste, bubble animation, dark theme
- Floating menu button

### ⚡ Performance

All under **Other Features → Performance**, all **on by default** (turn them off if you prefer):

- **Don't update dead players** — skips most `FixedUpdate` frames for **dead** players. This is the single biggest win in a full lobby. A skip-count slider (2–300, default 60) controls how aggressively this is applied. Slight trade-off: dead players' overlays refresh a little slower
- **Low load mode** — round-robin player updates: only one player is fully updated per frame while the rest are skipped. Only engages above 8 players; the local player and the host are never skipped
- **Hide console window** — hides the black BepInEx console window at startup
- A **per-second update scheduler** distributes the mod's own periodic work across frames instead of running it all in one frame, which removes the periodic frame-time spikes

### 🚀 Startup & Update Check

- **Update check on startup** — the mod checks for a newer version in the background and, if one is available, shows an "update available" hint in the mod menu. The check never blocks or delays the game's normal startup
- **Check Update button** — a button on the main menu, next to GitHub / QQ Group / Report Cheater. It reads the latest GitHub release, compares it with your version, and asks before doing anything
- **In-app download and replace** — if you accept, the new DLL is downloaded, verified and swapped in place; restart the game to finish. Nothing is replaced unless the download passes the checksum, size and assembly-name checks, and a failed swap rolls back to the previous file

### 🔍 Mod Client Detection

- Detects and marks other compatible mod clients in the room
- Shows the detected client's **Amethyst version** above their head in the lobby — the format is `♥ Ame{version} <User> ♥` (e.g. `♥ Ame1.1.7 用户 ♥` in Chinese, `♥ Ame1.1.7 User ♥` in English). Your own tag shows your version too
- The version travels in the same handshake that already identifies compatible clients, so it works without any extra traffic. Players on a build that does not announce a version still get the plain `♥ Ame User ♥` tag

### 🌐 Personalization

- Languages: Chinese / English / Русский
- Adjustable UI theme and scale; telemetry/crash reporting can be disabled

---

## Hotkeys

| Key | Action |
|:---:|:-------|
| `Insert` | Open / close the main menu |
| `F2` | Show / hide recap info (lobby and post-match) |
| `F6` | Copy the current room info |
| `M` | Open / close the music player |

**While the music player window is open**, these extra keys work (all configurable under **Settings → Hotkeys**):

| Key | Action |
|:---:|:-------|
| `[` / `MusicPrevKey` | Previous track |
| `]` / `MusicNextKey` | Next track |
| `P` / `MusicPlayPauseKey` | Play / pause |

> Hotkeys can be changed in the mod menu under **Settings → Hotkeys** (click a key badge, press the new key; `Esc` cancels, right-click or the trash icon clears it), or in `BepInEx/config/amethyst.mod.cfg` (`Keys.MenuKey` / `Keys.RecapKey` / `Keys.CopyCodeKey` / `Keys.MusicKey` / `Keys.MusicPrevKey` / `Keys.MusicNextKey` / `Keys.MusicPlayPauseKey`).

---

## Installation

> Amethyst requires **BepInEx (IL2CPP, Windows x64)**.

1. Download and install **BepInEx BleedingEdge — IL2CPP (`win-x64`)**
2. Locate your Among Us install folder:
   - **Steam**: Library → right-click Among Us → Manage → Browse local files
   - **Epic**: Library → Among Us → Manage, usually `C:\Program Files\Epic Games\AmongUs`
3. Extract BepInEx into the game folder (next to `Among Us.exe`)
4. Launch the game once to the main menu, then close it (this creates the `BepInEx/plugins` folder)
5. Put **`Amethyst_v1.1.7.dll`** into `BepInEx/plugins/`
6. Launch the game and press `Insert` to open the menu

> The first launch creates the config files under `BepInEx/config/`; delete the matching `.cfg` to reset your settings.

---

## Anti-Cheat Notices

An anti-cheat hit raises a notification and applies the configured action (warn / kick / ban). Common detections include, but are not limited to:

| Category | Description |
|:---------|:------------|
| Crewmate kill | A crewmate role initiating a kill |
| Impostor kills impostor | An impostor killing a teammate |
| Dead player kills | A dead player initiating a kill |
| Repeatedly killing a dead player | Killing an already-dead target over and over |
| Crewmate / off-map sabotage | A crewmate, or sabotage from an illegal position |
| Rapid sabotage | Sabotaging different systems in quick succession |
| Venting / sabotaging / closing doors in a meeting | Impossible behaviour during a meeting |
| Venting without permission / forged vent ID | Venting without the right, or with a fake vent ID |
| Venting on another player's behalf | Forcing another player into / out of a vent |
| Vent-kick exploit | Exploiting the vent-kick bug |
| Forced zipline | Forced zipline usage |
| Report / vote abuse | Reporting during a meeting, reporting yourself or a living player, voting outside a meeting |
| Forged host RPCs | Client-sent `SetRole` / `SetInfected` / `SetTasks` / `VotingComplete` / `SyncSettings` |
| Repair from a distance | Repairing a sabotage without standing near the console |
| Door spam | Closing more than one door within 0.01 s, or closing doors during a meeting |
| H&S report / sabotage / doors / illegal vent | Hide & Seek anomalies |
| Lobby game RPCs | Fake match RPCs (kills, meetings, shapeshifts) before the match starts |
| RPC flood | A single client spamming RPCs |

---

## Building From Source

You need Among Us assembly references (`Assembly-CSharp.dll` and friends). The project points at an extracted reference folder via `GameRefsDir`.

```
dotnet build src/Amethyst.csproj -c Release
```

The build output is `src/bin/Release/netcoreapp6.0/Amethyst.dll` (plus a versioned copy `Amethyst_v1.1.7.dll`).

---

## Reference Code & Credits

Amethyst's anti-cheat rules, RPC handling and UI behaviour were written with reference to the following open-source Among Us projects. Credit goes to their authors — please support them:

- **[BetterAmongUs](https://github.com/D1GQ/BetterAmongUs)** — RPC sender / net-object resolution and the overall anti-cheat structure
- **[OnyxMenu](https://github.com/Veltrix-s/OnyxMenu)** — RPC guards, teleport and AFK guards, menu animation and layout
- **[Hydra](https://github.com/MrDiamond64/Hydra)** — per-RPC validation, the `SendLateRejection` kick path, and the protection module layout
- **[EndlessHostRoles](https://github.com/Gurge44/EndlessHostRoles)** — role colours, host tools and UI patches
- **[SickoMenu](https://github.com/g0aty/SickoMenu)** — the SMAC anti-cheat rule set, used as a reference for rule coverage
- **[BanMod](https://github.com/GiannBart/BanMod)** — exploit guards, the meeting black-screen guard and anti-cheat modules

Each project remains the property of its respective author and is distributed under its own license. Amethyst does not bundle or redistribute their binaries.

---

## Disclaimer

Amethyst is an unofficial third-party modification for Among Us.

This mod is not affiliated with Among Us or Innersloth LLC, and the content contained therein is not endorsed or otherwise sponsored by Innersloth LLC. Portions of the materials contained herein are property of Innersloth LLC. © Innersloth LLC.

Amethyst is intended for use in private lobbies only.

The software is provided "as is", without warranty of any kind. Installing or using it means you accept any consequences that follow, including but not limited to account restrictions, bans, kicks, crashes, progress loss, file corruption, game instability, or incompatibility with future game updates. The developer is not responsible for any consequences, damages or misuse arising from the use of this software.

---

## License

This project is released under the **GNU GPL v3.0** (see [LICENSE](LICENSE)). You are free to use, study, share and modify it, but derivative works must use the same license.

---
# Русский

## Содержание

- [Обзор](#обзор)
- [Возможности](#возможности)
  - [Античит](#-античит)
  - [Защита вентиляции / зиплайнов](#-защита-вентиляции--зиплайнов)
  - [Управление игроками](#-управление-игроками)
  - [Сетевая защита](#-сетевая-защита)
  - [Защита в «Прятках»](#-защита-в-прятках)
  - [Комната и сессия](#-комната-и-сессия)
  - [Итоги матча](#-итоги-матча)
  - [Музыкальный плеер](#-музыкальный-плеер)
  - [Роли победителей](#-роли-победителей)
  - [Цвета ролей](#-цвета-ролей)
  - [Панель информации о роли](#-панель-информации-о-роли)
  - [Оптимизация интерфейса](#-оптимизация-интерфейса)
  - [Производительность](#-производительность)
  - [Запуск и проверка обновлений](#-запуск-и-проверка-обновлений)
  - [Определение модовых клиентов](#-определение-модовых-клиентов)
  - [Персонализация](#-персонализация)
- [Горячие клавиши](#горячие-клавиши)
- [Установка](#установка)
- [Уведомления античита](#уведомления-античита)
- [Сборка из исходников](#сборка-из-исходников)
- [Использованные проекты и благодарности](#использованные-проекты-и-благодарности)
- [Отказ от ответственности](#отказ-от-ответственности)
- [Лицензия](#лицензия)

---

## Обзор

Amethyst (紫晶) — античит-мод для Among Us, рассчитанный на приватные лобби. Он выявляет «невозможные RPC» (убийства от лица члена экипажа, саботаж от члена экипажа, вход в вентиляцию во время собрания, вход в вентиляцию за другого игрока, эксплойт «вентиляция-кик» и так далее), определяет, читерит ли игрок, и даёт хосту выбрать действие: **Нет / Предупреждение / Кик / Бан**. Кроме того, мод умеет определять вход и выход игроков, ведёт бан-лист и проверку уровня, а также добавляет множество функций комфорта на матч.

> Мод предназначен для хостов в их собственных приватных лобби. Пожалуйста, не используйте его в публичных лобби — это испортит игру остальным.

---

## Возможности

### 🛡 Античит

- **Детект RPC** — ловит невозможные RPC: убийства от члена экипажа, саботаж от члена экипажа, вход в вентиляцию без права, поддельные ID вентиляции, вход в вентиляцию / саботаж / закрытие дверей во время собрания, саботаж за пределами карты, несанкционированные превращение / исчезновение и прочие нарушения роли
- **Проверка репортов и голосов** — отклоняет репорты во время собрания, репорты на себя или на живого игрока, репорты мёртвым игроком либо из вентиляции / на лестнице / платформе / в момент превращения, а также голоса вне собрания
- **RPC хоста** — будучи хостом, отбрасывает поддельные пакеты `SetRole` / `SetInfected` / `SetTasks` / `VotingComplete` / `SyncSettings`; их отправляет только хост, поэтому получение такого пакета означает подделку со стороны клиента
- **Проверка ремонта саботажа** — ремонт света / кислорода / реактора / связи принимается, только если игрок действительно стоит у консоли (на стороне хоста, только уведомление)
- **Ограничение закрытия дверей** — закрытие дверей отбрасывается во время собраний и изгнания, а повторное закрытие в течение 0,01 с отклоняется (чит «закрыть всё одной кнопкой»)
- **Детект убийств** — предатель убивает предателя, мёртвый игрок начинает убийство, повторные убийства уже мёртвого игрока
- **Отбрасывание RPC** — будучи хостом, отбрасывает помеченные пакеты, чтобы они не сработали
- **Действия** — **Нет / Предупреждение / Кик / Бан**, плюс уровень **Ошибка-кик**, использующий штатный путь отказа `ClientTimeout`
- **Детект частоты RPC** — определяет клиента, заваливающего сервер RPC (порог 25/с, только уведомление)
- **Блокировка раннего собрания** — блокирует собрания и репорты в первые 15 секунд
- **Блокировка фальшивого собрания в лобби** — блокирует поддельный интерфейс собрания в лобби
- **Завершение без предателей** — если все предатели вышли или погибли, хост завершает раунд, а не оставляет игру на чёрном экране «нет предателей»

> Неизвестные ID RPC всегда разрешены. Обновления игры и моды ролей добавляют новые ID, и их блокировка приводила к ложным срабатываниям.

### 🚪 Защита вентиляции / зиплайнов

- **Правила вентиляции** — вход во время собрания, вход без права, поддельные ID, вход за другого игрока
- **Анти-форс вентиляция и анти-выброс** — блокирует принудительный проход и выброс, направленные на ваш клиент
- **Защита от эксплойта «вентиляция-кик»** — будучи хостом, наказывает клиент, отправивший пакет эксплойта
- **Анти-форс зиплайн** — блокирует принудительные поездки на зиплайне, направленные на ваш клиент

### 🕵 Управление игроками

- **Определение входа/выхода** — показывает ник, платформу и уровень подключающегося игрока
- **Бан-лист** — кик / бан при входе по FriendCode / PUID
- **Проверка уровня** — задаёт минимальный / максимальный уровень и действие
- **История игроков и история читов** (**всегда включено**, пишется только в файл и никогда не показывается в интерфейсе):
  - каждый вход и выход записывает имя / FriendCode / PUID / платформу / уровень в `Amethyst/PlayerHistory.txt`
  - каждое срабатывание античита записывает имя / FriendCode / PUID / платформу / причину в `Amethyst/CheatHistory.txt` (та же папка, что и бан-лист)
  - поддерживается **чтение истории по FriendCode** — так восстанавливаются имена игроков, вышедших резко

### 🌊 Сетевая защита

- Отбрасывает слишком большие пакеты GameData, предотвращая падения и перегрузку
- Отбрасывает подсообщения GameData с некорректным / повреждённым типом
- Проверяет VotingComplete на чрезмерный массив голосующих (перегрузка выделения памяти)
- **Защита от волны спавнов** — обрезает массовые спавны в одном кадре, предотвращая зависания и падения

### 🧩 Защита в «Прятках»

- Определяет и блокирует аномальные репорты, закрытие дверей, саботаж и незаконную вентиляцию в режиме «Прятки»

### 👥 Комната и сессия

- **Копировать информацию о комнате** (`F6`) — копирует текущую комнату в буфер обмена. **Текст** подчиняется языку мода, а **значения** всегда берутся из самой комнаты:

  ```
  ABCD(7/10)
  Режим:Классика
  Язык:Английский
  Хост:SomeName
  Сервер:Азия
  ```

  `Режим` — текущий режим комнаты (Классика / Прятки, включая варианты с Шутом), а `Язык` — языковая настройка **комнаты**: поэтому в англоязычной комнате при китайском моде будет `Язык:英语`, а при русском — `Язык:Английский`.
- **Показывать хоста на собраниях** — отображает хоста в левом верхнем углу во время собрания
- **Показ ролей** — после вашей смерти показывает роли всех игроков; при жизни можно показывать свою роль и роли союзников-предателей
- **Кик/бан во время матча** — хост может кикнуть или забанить прямо в матче
- **Перехват цвета** — только в лобби, автоматически занимает нужный цвет, когда он освобождается (опционально, по умолчанию выключено)
- **Автовозврат в лобби** и **NoWin** (никогда не завершать матч)
- **Вкладка разработчика** (`Developer`) — **зонд производительности** и **условия без победы**, вынесены отдельно от обычных настроек

### 📋 Итоги матча

**Выезжающая панель итогов** (закреплена слева, не перетаскивается), показывающая по каждому игроку **прошлого матча**: роль (жив `=>` мёртв), число убийств / прогресс заданий, причину смерти (убит — с указанием убийцы / изгнан / отключился / погиб / выжил) и результат матча.

- Копирование в виде простого текста одним нажатием
- Кнопка **Показать/скрыть итоги** находится в левом верхнем углу экрана; горячая клавиша `F2` делает то же самое
- Доступна и в лобби, и на экране результатов (больше не открывается автоматически, по умолчанию скрыта)
- Во время матча при жизни остаётся скрытой — показ чужих ролей по ходу игры был бы читерством

### 🎵 Музыкальный плеер

Встроенный музыкальный плеер (`M`), который проигрывает **ваши собственные аудиофайлы** во время игры.

**Куда класть треки:**

```
<папка Among Us>\Amethyst\Music\
```

Это та же папка `Amethyst`, где лежат бан-лист, `PlayerHistory.txt` и `CheatHistory.txt`. Она создаётся автоматически при первом использовании, а её путь всегда показан **внутри окна плеера**, так что угадывать не нужно.

| Формат | Расширения |
|:-------|:-----------|
| MP3 | `.mp3` |
| WAV | `.wav` |
| Ogg Vorbis | `.ogg` |

**Возможности**

- **Плейлист** — все треки в папке, отсортированы по имени; нажмите на строку, чтобы включить
- **Управление** — предыдущий / play-pause / следующий / стоп, плюс кнопка **повтора**, переключающая **один трек → плейлист → один раз**
- **Полоса прогресса** — стилизованный слайдер с индикацией времени (клик или перетаскивание с **отпусканием** для перехода; позиция применяется только при отпускании, поэтому перетаскивание не заикается)
- **Ползунок громкости** с кнопками − / + для точной подстройки, в стиле остального меню мода (0–100 %)
- **Обновление** — повторное сканирование папки, поэтому файлы, добавленные **во время работы игры**, появляются сразу
- **Запоминает** позицию окна (тяните за заголовок), громкость и последний режим повтора между запусками
- **Автопереход** — по окончании играет следующий трек; в режиме **один раз** плеер останавливается после последнего трека
- Строка **сейчас играет** всегда показывает **`Сейчас играет <название>`** (или `Пауза <название>`), так что текущий трек виден сразу
- **Заглушает музыку игры**, пока играет плеер — музыка лобби и главного меню отключается, чтобы не спорить с вашим треком, и возвращается, как только вы ставите паузу, останавливаете или уводите громкость в ноль. Отдельный переключатель: **Настройки → Лобби и список комнат → Заглушать музыку игры во время проигрывания** (`Music.MuteGameMusic`, включён по умолчанию). Звуковые эффекты окружения намеренно **не** трогаются
- Декодирование идёт в фоновом потоке, поэтому запуск большого трека не подвешивает игру
- Треки длиннее **20 минут** отклоняются (декодированное аудио держится в памяти — это его ограничивает)

**Примечания**

- Плеер **локальный, на стороне клиента**: музыку слышите только вы, в сеть ничего не уходит
- Аудио декодируется двумя встроенными сторонними библиотеками с открытым кодом (**NAudio** для MP3/WAV и **NVorbis** для OGG, обе MIT). Они встроены в `Amethyst.dll` и распаковываются в `Amethyst\libs\` при первом использовании, поэтому **никаких дополнительных загрузок не нужно**. Тексты лицензий лежат в той же папке — см. `THIRD-PARTY-NOTICES.txt`
- Если трек не загрузился, окно показывает `Load failed`; ищите в `BepInEx\LogOutput.log` строку с `[Music]` и причиной

### 🏆 Роли победителей

- На экране результатов **роли победителей** показываются **под их именами** на аватарах
- Список победителей берётся из кеша самой игры, а роли — из внутриигрового снимка мода, поэтому всё работает и после смены сцены конца матча
- Отключается отдельно в **Другие функции → Оптимизация интерфейса**

### 🎮 Цвета ролей

Каждая **ванильная роль** отрисовывается своим цветом вместо сплошного красного предателя / синего члена экипажа.

| Роль | Цвет |
|:-----|:------|
| Член экипажа | `#8CFFFF` |
| Предатель | `#C61111` |
| Инженер | `#FF6A00` |
| Учёный | `#8EE98E` |
| Ангел-хранитель | `#77E6D1` |
| Трекер | `#34AD50` |
| Шумовик | `#FF4A62` |
| Детектив | `#625EEE` |
| Проводник душ (Инфлюенсер) | `#FF0066` |
| Оборотень | `#C61111` |
| Фантом | `#C61111` |
| Гадюка | `#C61111` |
| Призрак предателя | `#C61111` |
| Призрак члена экипажа | `#8CFFFF` |
| Судья | `#F8D85A` |

- Применяется к **экрану выдачи ролей** (вступительный текст «Ты — …»), **имени роли над головой**, собраниям, чату и панели информации о роли
- Любая роль, добавленная обновлением игры, автоматически получает цвет своей команды, а самопроверка при запуске сообщает о ролях без собственного цвета

### 📖 Панель информации о роли

Внутриигровая панель (похожа на панель заданий, сворачивается и разворачивается нажатием), показывающая вашу роль и её описание.

- У каждой из 15 ванильных ролей есть **короткая атмосферная строка** и **подробное описание правил**, **полностью локализованные на китайский, английский и русский**
- Определение призрачных ролей использует `RoleManager.IsGhostRole` самой игры, поэтому роли из будущих обновлений подхватываются автоматически
- Пока вы живы и играете за члена экипажа, видна только ваша роль; после смерти (или играя за предателя) дополнительно перечисляются игроки, которых вам разрешено видеть

### 🎨 Оптимизация интерфейса

> Анимация выезда правой панели главного меню встроена и **работает всегда**.

У каждой функции этой группы **свой независимый переключатель** в **Другие функции → Оптимизация интерфейса**:

- **Оптимизация главного меню** — убирает визуальный шум (шум фона, заливку и разделители левой панели, подсветку окон, полноэкранный оттенок, значок запросов в друзья), при этом **логотип Among Us сохраняется**
- **Выезд главного меню** — при нажатии «Играть / Мой аккаунт / Авторы» правая панель выезжает справа; при открытии настроек, объявлений, инвентаря или магазина она уезжает, чтобы не перекрывать окно
- **Кнопки-ссылки главного меню** — три кнопки в левом нижнем углу главного меню (**GitHub**, **QQ-группа** и **Пожаловаться на читера**), сделанные по одному шаблону и выглядящие одинаково. **Пожаловаться на читера** открывает страницу жалоб **`api2.elauk.top/acban/submit`** в браузере, копирует ссылку в буфер как запасной вариант и показывает уведомление. Все три скрываются переключателем `Other.MainMenuButtons`
- **Метка роли на собрании** — на собрании у каждого **живого** игрока рядом с аватаром есть значок `EditTag` (у мёртвых значка нет, а их прежняя метка скрывается). Нажатие открывает штатное меню оборотня и помечает игрока ролью; метка видна **рядом с названием собрания** и **над головой игрока в игре**. Предатели идут первыми красным, члены экипажа за ними синим, в конце — пункт «снять метку». Все метки автоматически сбрасываются по завершении матча (только локально — видите их лишь вы; в вашей собственной ячейке значка нет)
- **Итоги матча** — нажмите `F2` в лобби, чтобы посмотреть прошлый матч (см. [Итоги матча](#-итоги-матча) выше)
- **Панель заданий на собраниях** — список заданий остаётся видимым во время собраний и поднимается над интерфейсом собрания
- Принудительный показ кнопки старта, пропуск анимации убийства
- **Улучшенный показ пинга** — внутриигровые показатели с **выбором элементов**: задержка, FPS, сервер/регион, **код комнаты + (игроки/максимум)** и **имя хоста**. Включайте и выключайте каждый в **Другие функции → Кулдауны и пинг**; невыбранные просто не отображаются
- **Показ сервера** — показывает ваш регион/сервер с локализацией по языку (например, "Asia" / 「亚洲」 / «Азия»), без сокращений
- **Информация о комнате** — список поиска игры показывает до 10 лобби в прокручиваемом списке (прокрутка **ровно на одну строку**, поэтому строки никогда не выходят за пределы области), в каждой строке — имя хоста / платформа / код комнаты
- **Показ названия цвета** — рядом с игроком отображается название его цвета
- **Цвета имён** — ваше имя и имена других игроков отрисовываются их цветом; с точки зрения предателя союзники показываются красным (действует на пузыри чата, голосования на собрании и вступительный экран «кто погиб / кто нашёл тело»; экран результатов изгнания сохраняет родной вид игры)
- Опции отображения: показывать хоста в левом верхнем углу собрания, показывать свою роль, показывать роли союзников-предателей, показывать роли всех после вашей смерти
- **Сохранение образов** — 6 кнопок-пресетов на странице инвентаря для переключения образа одним нажатием
- **Разблокировка 240 FPS**, **разблокировка всех косметических предметов**, **пропуск штрафа за отключение** и **автовозврат в лобби** — у каждого **свой независимый переключатель** в разделе «Отображение и роли», они не зависят от других переключателей
- **Улучшенный чат** — поле с форматированием, копирование/вставка, анимация пузырей, тёмная тема
- Плавающая кнопка меню

### ⚡ Производительность

Всё находится в **Другие функции → Производительность**, всё **включено по умолчанию** (отключите, если не нужно):

- **Не обновлять мёртвых игроков** — пропускает большинство кадров `FixedUpdate` для **мёртвых** игроков. Это самый большой выигрыш в полном лобби. Ползунок числа пропусков (2–300, по умолчанию 60) задаёт агрессивность. Небольшая плата: оверлеи мёртвых игроков обновляются чуть медленнее
- **Режим низкой нагрузки** — поочерёдное обновление игроков: за кадр полностью обновляется только один игрок, остальные пропускаются. Включается только при более чем 8 игроках; локальный игрок и хост не пропускаются никогда
- **Скрыть окно консоли** — прячет чёрное окно консоли BepInEx при запуске
- **Планировщик раз в секунду** распределяет периодическую работу мода по кадрам вместо одного кадра, что убирает периодические скачки времени кадра

### 🚀 Запуск и проверка обновлений

- **Проверка обновлений при запуске** — мод в фоне проверяет новую версию и, если она есть, показывает подсказку в меню мода. Проверка никогда не блокирует и не задерживает обычный запуск игры
- **Кнопка проверки обновлений** — кнопка в главном меню, рядом с GitHub / QQ-группой / жалобой на читера. Она читает последний релиз на GitHub, сравнивает с вашей версией и спрашивает перед любыми действиями
- **Загрузка и замена внутри приложения** — при согласии новая DLL скачивается, проверяется и подменяется на месте; для завершения перезапустите игру. Ничего не заменяется, если загрузка не прошла проверки контрольной суммы, размера и имени сборки, а неудачная замена откатывается к предыдущему файлу

### 🔍 Определение модовых клиентов

- Определяет и помечает других совместимых модовых клиентов в комнате
- Показывает **версию Amethyst** обнаруженного клиента над головой в лобби — формат `♥ Ame{версия} <User> ♥` (например, `♥ Ame1.1.7 用户 ♥` на китайском, `♥ Ame1.1.7 User ♥` на английском). Ваша собственная метка тоже показывает версию
- Версия передаётся в том же рукопожатии, которое уже опознаёт совместимые клиенты, поэтому работает без дополнительного трафика. Игроки на сборке без объявления версии всё равно получают простую метку `♥ Ame User ♥`

### 🌐 Персонализация

- Языки: китайский / английский / русский
- Настраиваемая тема и масштаб интерфейса; телеметрию и отправку отчётов о падениях можно отключить

---

## Горячие клавиши

| Клавиша | Действие |
|:---:|:-------|
| `Insert` | Открыть / закрыть главное меню |
| `F2` | Показать / скрыть итоги матча (лобби и экран результатов) |
| `F6` | Скопировать информацию о текущей комнате |
| `M` | Открыть / закрыть музыкальный плеер |

**Пока открыто окно плеера**, работают дополнительные клавиши (все настраиваются в **Настройки → Горячие клавиши**):

| Клавиша | Действие |
|:---:|:-------|
| `[` / `MusicPrevKey` | Предыдущий трек |
| `]` / `MusicNextKey` | Следующий трек |
| `P` / `MusicPlayPauseKey` | Играть / пауза |

> Горячие клавиши меняются в меню мода в **Настройки → Горячие клавиши** (нажмите на плашку клавиши и введите новую; `Esc` отменяет, правый клик или значок корзины очищает), либо в `BepInEx/config/amethyst.mod.cfg` (`Keys.MenuKey` / `Keys.RecapKey` / `Keys.CopyCodeKey` / `Keys.MusicKey` / `Keys.MusicPrevKey` / `Keys.MusicNextKey` / `Keys.MusicPlayPauseKey`).

---

## Установка

> Amethyst требует **BepInEx (IL2CPP, Windows x64)**.

1. Скачайте и установите **BepInEx BleedingEdge — IL2CPP (`win-x64`)**
2. Найдите папку установки Among Us:
   - **Steam**: Библиотека → правый клик по Among Us → Управление → Просмотреть локальные файлы
   - **Epic**: Библиотека → Among Us → Управление, обычно `C:\Program Files\Epic Games\AmongUs`
3. Распакуйте BepInEx в папку игры (рядом с `Among Us.exe`)
4. Запустите игру до главного меню и закройте её (так создастся папка `BepInEx/plugins`)
5. Положите **`Amethyst_v1.1.7.dll`** в `BepInEx/plugins/`
6. Запустите игру и нажмите `Insert`, чтобы открыть меню

> При первом запуске в `BepInEx/config/` создаются файлы конфигурации; удалите соответствующий `.cfg`, чтобы сбросить настройки.

---

## Уведомления античита

Срабатывание античита показывает уведомление и применяет заданное действие (предупреждение / кик / бан). Типичные обнаружения включают, но не ограничиваются:

| Категория | Описание |
|:---------|:------------|
| Убийство членом экипажа | Член экипажа инициирует убийство |
| Предатель убивает предателя | Предатель убивает союзника |
| Убийство мёртвым игроком | Мёртвый игрок инициирует убийство |
| Повторные убийства мёртвого игрока | Убийство уже мёртвой цели снова и снова |
| Саботаж членом экипажа / вне карты | Саботаж от члена экипажа или из недопустимой позиции |
| Быстрый саботаж | Саботаж разных систем подряд |
| Вентиляция / саботаж / двери на собрании | Невозможное поведение во время собрания |
| Вентиляция без права / поддельный ID | Вход без права или с фальшивым ID вентиляции |
| Вентиляция за другого игрока | Принудительный вход / вывод другого игрока из вентиляции |
| Эксплойт «вентиляция-кик» | Использование бага вентиляции-кика |
| Принудительный зиплайн | Принудительное использование зиплайна |
| Злоупотребление репортами / голосами | Репорт на собрании, репорт на себя или на живого игрока, голос вне собрания |
| Поддельные RPC хоста | Отправленные клиентом `SetRole` / `SetInfected` / `SetTasks` / `VotingComplete` / `SyncSettings` |
| Ремонт на расстоянии | Ремонт саботажа без нахождения у консоли |
| Спам дверями | Закрытие более одной двери за 0,01 с или закрытие дверей на собрании |
| Прятки: репорт / саботаж / двери / вентиляция | Аномалии в режиме «Прятки» |
| RPC матча в лобби | Фальшивые RPC матча (убийства, собрания, превращения) до его начала |
| Поток RPC | Один клиент заваливает сервер RPC |

---

## Сборка из исходников

Нужны ссылки на сборки Among Us (`Assembly-CSharp.dll` и другие). Проект указывает на распакованную папку ссылок через `GameRefsDir`.

```
dotnet build src/Amethyst.csproj -c Release
```

Результат сборки — `src/bin/Release/netcoreapp6.0/Amethyst.dll` (плюс копия с версией `Amethyst_v1.1.7.dll`).

---

## Использованные проекты и благодарности

Правила античита, обработка RPC и поведение интерфейса Amethyst написаны с опорой на следующие открытые проекты для Among Us. Благодарность их авторам — поддержите их:

- **[BetterAmongUs](https://github.com/D1GQ/BetterAmongUs)** — отправка RPC / разрешение сетевых объектов и общая структура античита
- **[OnyxMenu](https://github.com/Veltrix-s/OnyxMenu)** — защиты RPC, телепорта и AFK, анимация и компоновка меню
- **[Hydra](https://github.com/MrDiamond64/Hydra)** — поштучная проверка RPC, путь кика через `SendLateRejection` и структура модулей защиты
- **[EndlessHostRoles](https://github.com/Gurge44/EndlessHostRoles)** — цвета ролей, инструменты хоста и патчи интерфейса
- **[SickoMenu](https://github.com/g0aty/SickoMenu)** — набор правил античита SMAC, использованный как ориентир по охвату правил
- **[BanMod](https://github.com/GiannBart/BanMod)** — защиты от эксплойтов, защита от чёрного экрана на собрании и модули античита

Каждый проект остаётся собственностью своего автора и распространяется по своей лицензии. Amethyst не включает и не перераспространяет их бинарные файлы.

---

## Отказ от ответственности

Amethyst — неофициальная сторонняя модификация Among Us.

Мод не связан с Among Us или Innersloth LLC, а его содержимое не одобрено и не спонсируется Innersloth LLC. Часть материалов принадлежит Innersloth LLC. © Innersloth LLC.

Amethyst предназначен только для приватных лобби.

Программное обеспечение предоставляется «как есть», без каких-либо гарантий. Устанавливая или используя его, вы принимаете любые последствия, включая, помимо прочего, ограничения аккаунта, баны, кики, падения, потерю прогресса, повреждение файлов, нестабильность игры или несовместимость с будущими обновлениями. Разработчик не несёт ответственности за любые последствия, ущерб или злоупотребления, возникшие в результате использования этого программного обеспечения.

---

## Лицензия

Проект распространяется по **GNU GPL v3.0** (см. [LICENSE](LICENSE)). Вы можете свободно использовать, изучать, распространять и изменять его, но производные работы должны быть под той же лицензией.

---

<div align="center">

Made with ❤️ by **一只屑小紫**

</div>
