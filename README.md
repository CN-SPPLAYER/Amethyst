<div align="center">

# Amethyst v1.1.6
<img width="1602" height="898" alt="d775916d6ada8995bc97655354dc7338" src="https://github.com/user-attachments/assets/1d7ba60f-5f79-494d-a9ab-a22461f07588" />

**An Among Us anti-cheat and utility mod**

A BepInEx (IL2CPP) client-side mod for Among Us that provides RPC anti-cheat, player management, network protection and a broad set of in-game/lobby quality-of-life features. Intended for hosts who want to keep cheaters out of their own private lobbies.

<br>

<img src="https://img.shields.io/badge/Among%20Us-IL2CPP-000000?style=for-the-badge&logo=amongus&logoColor=white" alt="Among Us">
<img src="https://img.shields.io/badge/BepInEx-6.x-5865F2?style=for-the-badge" alt="BepInEx">
<img src="https://img.shields.io/badge/.NET-6.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET">
<img src="https://img.shields.io/badge/version-1.1.6-9b59b6?style=for-the-badge" alt="version">
<img src="https://img.shields.io/badge/License-GPL--3.0-blue?style=for-the-badge&logo=gnu&logoColor=white" alt="License">

</div>

---

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
- **Kill detection** — impostor killing an impostor, a dead player initiating a kill, repeatedly killing an already-dead player
- **RPC dropping** — as host, drops the flagged RPC packets so they never take effect
- **Actions** — choose **None / Warn / Kick / Ban** for the cheater
- **RPC flood detection** — detects a single client spamming RPCs
- **Early meeting blocking** — blocks meetings / reports during the first 15 seconds
- **Fake lobby meeting blocking** — blocks spoofed meeting UI in the lobby

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
- **Transport** — previous / play-pause / next / stop, plus a **loop** toggle
- **Volume slider** styled like the rest of the mod's menu (0–100%)
- **Refresh** — re-scans the folder, so files dropped in **while the game is running** appear immediately
- **Remembers** both the window position (drag the title bar) and the volume between launches
- **Auto-advance** to the next track when one finishes (with loop off)
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
- **Meeting role tag** — in a meeting each player has an `EditTag` icon next to their avatar. Click it to open the game's own shapeshifter menu and mark that player with a role; the mark appears **next to the meeting name** and **above the player's head in game**. Impostors are listed first in red, crewmates after them in blue, with a "clear tag" entry at the end. All tags are cleared automatically when the match ends (local only — only you can see them; your own slot shows no icon)
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

### 🔍 Mod Client Detection

- Detects and marks other compatible mod clients in the room

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

**While the music player window is open**, these extra keys work:

| Key | Action |
|:---:|:-------|
| `[` | Previous track |
| `]` | Next track |
| `P` | Play / pause |

> Hotkeys can be changed in the mod menu under **Settings → Hotkeys** (click a key badge, press the new key; `Esc` cancels, right-click or the trash icon clears it), or in `BepInEx/config/amethyst.mod.cfg` (`Keys.MenuKey` / `Keys.RecapKey` / `Keys.CopyCodeKey` / `Keys.MusicKey`).

---

## Installation

> Amethyst requires **BepInEx (IL2CPP, Windows x64)**.

1. Download and install **BepInEx BleedingEdge — IL2CPP (`win-x64`)**
2. Locate your Among Us install folder:
   - **Steam**: Library → right-click Among Us → Manage → Browse local files
   - **Epic**: Library → Among Us → Manage, usually `C:\Program Files\Epic Games\AmongUs`
3. Extract BepInEx into the game folder (next to `Among Us.exe`)
4. Launch the game once to the main menu, then close it (this creates the `BepInEx/plugins` folder)
5. Put **`Amethyst_v1.1.6.dll`** into `BepInEx/plugins/`
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
| H&S report / sabotage / doors / illegal vent | Hide & Seek anomalies |
| Lobby game RPCs | Fake match RPCs (kills, meetings, shapeshifts) before the match starts |
| RPC flood | A single client spamming RPCs |

---

## Building From Source

You need Among Us assembly references (`Assembly-CSharp.dll` and friends). The project points at an extracted reference folder via `GameRefsDir`.

```
dotnet build src/Amethyst.csproj -c Release
```

The build output is `src/bin/Release/netcoreapp6.0/Amethyst.dll` (plus a versioned copy `Amethyst_v1.1.6.dll`).

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

<div align="center">

Made with ❤️ by **一只屑小紫**

</div>
