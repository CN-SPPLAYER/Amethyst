<div align="center">

# Amethyst
<img width="2021" height="1148" alt="Amethyst_MainMenuArt_Logo" src="https://github.com/user-attachments/assets/cfd02850-f4ae-4520-b929-386ec97ceef0" />


**An anti-cheat and utility mod for Among Us**

A client-side integration mod for Among Us built upon BepInEx (IL2CPP), providing anti-cheat protection and game optimisation. It is intended for hosts who wish to prevent their lobbies from being disrupted by malicious players.

<br>

<img src="https://img.shields.io/badge/Among%20Us-IL2CPP-000000?style=for-the-badge&logo=amongus&logoColor=white" alt="Among Us">
<img src="https://img.shields.io/badge/BepInEx-6.x-5865F2?style=for-the-badge" alt="BepInEx">
<img src="https://img.shields.io/badge/.NET-6.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET">
<img src="https://img.shields.io/badge/version-1.1.8-9b59b6?style=for-the-badge" alt="version">
<img src="https://img.shields.io/badge/License-GPL--3.0-blue?style=for-the-badge&logo=gnu&logoColor=white" alt="License">

</div>

---

## Overview

Amethyst is an anti-cheat and optimisation mod for Among Us. It guards against attacks that crash the lobby by detecting anomalous RPCs issued by players, and it includes more than 24 minor quality-of-life features.

> **Note:** The anti-cheat capability of Amethyst depends on host authority. If you are not the host, the majority of the anti-cheat features will not take effect.

---

## Features

### Anti-cheat

- **RPC detection** — captures anomalous RPCs.
- **Anomalous RPCs** — blocking may be enabled or disabled.
- **Cheater response** — selectable between **None**, **Warn**, **Invalid kick**, **Kick** and **Ban**. For kick and ban, a threshold may be configured for the number of anti-cheat detections required before the action is applied.

### Player management

- **Ban list** — kicks or bans players upon joining, by Friend Code or PUID.
- **Player history** — every player who joins or leaves the lobby is recorded to `Amethyst/PlayerHistory.txt`.
- **Cheat history** — every player who triggers the anti-cheat is recorded to `Amethyst/CheatHistory.txt`.

---

## Hotkeys

| Key | Action |
|:---:|:-------|
| `Insert` | Open or close the main menu |
| `F2` | Show or hide recap information |
| `F6` | Copy the current lobby information |
| `M` | Open or close the music player |

> All hotkeys may be reassigned in the mod settings.

---

## Referenced Code and Acknowledgements

Portions of the code, user interface and assets of Amethyst are derived from the following open-source Among Us projects. Their authors are gratefully acknowledged. Should you not wish for your assets to be used, please contact me and they will be removed.

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

## Disclaimer

This mod is not affiliated with Among Us or Innersloth LLC, and its content is neither endorsed nor sponsored by Innersloth LLC. Portions of the materials contained herein are the property of Innersloth LLC. © Innersloth LLC.

This mod is provided "as is", without warranty of any kind. Installation or use constitutes acceptance of all consequences arising therefrom, including but not limited to account restrictions, bans, kicks, crashes, loss of progress, file corruption, game instability, or incompatibility with future game updates. The developer accepts no responsibility for any consequence, damage or misuse arising from the use of this mod.

---

## License

This project is released under the **GNU GPL v3.0** (see [LICENSE](LICENSE)). You are free to use, study, share and modify it, provided that derivative works are distributed under the same license.
