<div align="center">

# Amethyst v1.1.8
<img width="1602" height="898" alt="d775916d6ada8995bc97655354dc7338" src="https://github.com/user-attachments/assets/1d7ba60f-5f79-494d-a9ab-a22461f07588" />

**一款《Among Us》反作弊与实用模组**

一个基于BepInEx(IL2CPP)的《Among Us》客户端整合模组，提供反作弊和游戏优化功能，推荐希望在自己的房间中避免被炸房的房主使用。

<br>

<img src="https://img.shields.io/badge/Among%20Us-IL2CPP-000000?style=for-the-badge&logo=amongus&logoColor=white" alt="Among Us">
<img src="https://img.shields.io/badge/BepInEx-6.x-5865F2?style=for-the-badge" alt="BepInEx">
<img src="https://img.shields.io/badge/.NET-6.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET">
<img src="https://img.shields.io/badge/version-1.1.8-9b59b6?style=for-the-badge" alt="version">
<img src="https://img.shields.io/badge/License-GPL--3.0-blue?style=for-the-badge&logo=gnu&logoColor=white" alt="License">

</div>

---

## 概述

Amethyst是一款的《Among Us》反作弊以及优化模组。它通过检测玩家发出的异常RPC来防御外挂炸房，以及24+游戏优化小功能。

> **注意：** Amethyst的反作弊依赖房主身份。如果你不是房主，那么大部分反作弊功能将失效。

---

## 功能

### 反作弊

- **RPC检测** —— 捕获异常RPC
- **异常RPC** —— 可选阻止或不阻止
- **作弊者处理方式** —— 可选无/警告/错误踢出/踢出/封禁，注意，踢出和封禁可选触发反作弊达到多少次之后才执行

### 玩家管理

- **封禁列表** —— 按FriendCode/PUID在加入时踢出/封禁
- **玩家历史** —— 每次加入房间或离开的玩家都将记录到此Amethyst/PlayerHistory.txt
- **作弊历史** —— 每次触发反作弊的玩家都将记录到此Amethyst/CheatHistory.txt

---

## 热键

| 按键 | 操作 |
|:---:|:-------|
| `Insert` | 打开/关闭主菜单 |
| `F2` | 显示/隐藏回顾信息 |
| `F6` | 复制当前房间信息 |
| `M` | 打开/关闭音乐播放器 |

> 按键可以从模组设置中更改。

---

## 参考代码与致谢

Amethyst的部分代码、UI以及部分资产取自以下开源Among Us项目。感谢其创作者，如果你的资产不想让我使用，请联系我，我将删除它。

- [BetterAmongUs](https://github.com/D1GQ/BetterAmongUs)
- [OnyxMenu](https://github.com/Veltrix-s/OnyxMenu)
- [Hydra](https://github.com/MrDiamond64/Hydra)
- [EndlessHostRoles](https://github.com/Gurge44/EndlessHostRoles)
- [SickoMenu](https://github.com/g0aty/SickoMenu)
- [BanMod](https://github.com/GiannBart/BanMod)
- [MiraAPI](https://github.com/All-Of-Us-Mods/MiraAPI)
- [TownofNext-Edited](https://github.com/qin-qwq/TownofNext-Edited)
- [AUnlocker](https://github.com/astra1dev/AUnlocker)
- [MalumMenu](https://github.com/scp222thj/MalumMenu)
- [Reactor](https://github.com/NuclearPowered/Reactor)
- [Nebula](https://github.com/Dolly1016/Nebula)
- [FinalSuspect](https://github.com/Slok7565/FinalSuspect)

---

## 免责声明

本模组与Among Us或Innersloth LLC无关，其内容未经Innersloth LLC认可或赞助。本文所含材料的部分内容为Innersloth LLC的财产。© Innersloth LLC。

本模组按“原样”提供，不附带任何形式的保证。安装或使用即表示你接受随之而来的一切后果，包括但不限于账户限制、封禁、踢出、崩溃、进度丢失、文件损坏、游戏不稳定或与未来游戏更新不兼容。开发者不对因使用本模组而产生的任何后果、损害或滥用负责。

---

## 许可证

本项目基于**GNU GPL v3.0**发布（见[LICENSE](LICENSE)）。你可自由使用、研究、分享和修改，但衍生作品必须使用相同许可证。
