<div align="center">

# Amethyst v1.1.5
<img width="1602" height="898" alt="d775916d6ada8995bc97655354dc7338" src="https://github.com/user-attachments/assets/1d7ba60f-5f79-494d-a9ab-a22461f07588" />

**An Among Us anti-cheat and utility mod**

A BepInEx (IL2CPP) client-side mod for Among Us that provides RPC anti-cheat, player management, network protection and a broad set of in-game/lobby quality-of-life features. Intended for hosts who want to keep cheaters out of their own private lobbies.

<br>

<img src="https://img.shields.io/badge/Among%20Us-IL2CPP-000000?style=for-the-badge&logo=amongus&logoColor=white" alt="Among Us">
<img src="https://img.shields.io/badge/BepInEx-6.x-5865F2?style=for-the-badge" alt="BepInEx">
<img src="https://img.shields.io/badge/.NET-6.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET">
<img src="https://img.shields.io/badge/version-1.1.5-9b59b6?style=for-the-badge" alt="version">
<img src="https://img.shields.io/badge/License-GPL--3.0-blue?style=for-the-badge&logo=gnu&logoColor=white" alt="License">

</div>

---

## Overview

Amethyst (紫晶) is an Among Us anti-cheat mod for private lobbies. It detects "impossible RPCs" (crewmate kills, crewmate sabotage, venting during a meeting, venting on another player's behalf, the vent-kick exploit, and so on), decides whether a player is cheating, and lets the host choose an action: **None / Warn / Kick / Ban**. It also bundles join/leave detection, a ban list, a level guard, and many match quality-of-life features.

> This mod is intended for hosts to use in their own private lobbies. Please do not use it in public lobbies, as it would ruin the experience for others.

---

## Features

### 🛡 Anti-Cheat

- **RPC detection**: catches impossible RPCs (crewmate kills, crewmate sabotage, venting without permission, forged vent IDs, venting/sabotage/door-closing during meetings, sabotage outside the map, unauthorized shapeshift/vanish, and other role violations)
- **Kill detection**: impostor killing an impostor, a dead player initiating a kill, repeatedly killing an already-dead player
- **RPC dropping**: as host, drops the flagged RPC packets so they never take effect
- **Actions**: choose **None / Warn / Kick / Ban** for the cheater
- **RPC flood detection**: detects a single client spamming RPCs
- **Early meeting blocking**: blocks meetings/reports during the first 15 seconds
- **Fake lobby meeting blocking**: blocks spoofed meeting UI in the lobby

### 🚪 Vent / Zipline Protection

- **Vent rules**: venting during a meeting, venting without permission, forged vent IDs, venting on another player's behalf
- **Anti force-vent / anti vent-eject**: blocks forced passthrough and ejection aimed at your client
- **Vent-kick exploit protection**: as host, punishes the client that sends the exploit packet
- **Anti forced zipline**: blocks forced zipline rides aimed at your client

### 🕵 Player Management

- **Join/leave detection**: shows a joining player's nickname, platform and level
- **Ban list**: kick/ban on join by FriendCode / PUID
- **Level guard**: set a minimum / maximum level and choose an action
- **Player history / cheat history** (**always on**, written to file only and never shown in any UI):
  every join/leave records name / friend code / PUID / platform / level to `Amethyst/PlayerHistory.txt`;
  every anti-cheat hit records name / friend code / PUID / platform / reason to `Amethyst/CheatHistory.txt` (same folder as the ban list).
  Supports **reading history back** by friend code, which is used to recover the names of players who left abruptly

### 🌊 Network Protection

- Drops oversized GameData packets to prevent large-message crashes/overload
- Drops GameData sub-messages with an invalid/malformed type
- Guards VotingComplete against an oversized voter array (allocation overload)
- **Spawn flood protection**: trims a flood of spawns in a single frame to prevent freezes/crashes

### 🧩 Hide & Seek Protection

- Detects and blocks abnormal reports, door closes, sabotage and illegal venting in H&S

### 👥 Room & Session

- **Copy room info** (`F6`): copies the current room to your clipboard. The wording follows the **mod's language**, while the **values** always come from the **room itself**:
  ```
  ABCD(7/10)
  Mode:Classic
  Language:English
  Host:SomeName
  Server:Asia
  ```
  `Mode` is the room's current game mode (Classic / Hide & Seek, including the Fools variants) and `Language` is the **room's** language setting — so an English room reads `Language:英语` if your mod is set to Chinese, and `Language:Английский` if it is set to Russian.
- **Show host in meetings**: shows the host in the top-left corner during a meeting
- **Role display**: shows everyone's role after you die; while alive you can show your own role and your impostor teammates' roles
- **Kick/ban during a match**: the host can kick/ban mid-match
- **Color snipe**: lobby only, automatically claims your desired color when it becomes free (optional, off by default)
- **Auto return to lobby** and **NoWin** (never end the match)
- **Developer tab** (`Developer`): **performance probe** and **no-win-conditions**, kept out of the way of the normal settings

### 📋 Recap Info

- A **sliding-in recap panel** (it is docked, not draggable) showing, for every player in the **previous match**:
  role (alive `=>` dead), kill count / task progress, cause of death (killed with the killer listed / ejected / disconnected / died / survived) and the match result
- One-click copy as plain text
- **New — toggle button**: a **Show/Hide recap info** button sits in the top-left corner of the screen. Use it or the `F2` hotkey to show/hide the window
- **New — lobby and post-match**: available both in the lobby and on the post-match screen (it no longer opens automatically; it starts hidden)
- While alive during a match it stays hidden — showing other players' roles mid-match would be cheating

### 🎵 Music Player

A built-in music player that plays **your own audio files** from disk while you play.

**Where to put your music**

```
<Among Us folder>\Amethyst\Music\
```

That is the same `Amethyst` folder that holds the ban list, `PlayerHistory.txt` and `CheatHistory.txt`. The folder is created automatically the first time the player opens (or on first launch if it does not exist yet). The path is shown **inside the player window** at all times, so you never have to guess.

**Supported formats**

| Format | Extensions |
|:-------|:-----------|
| MP3 | `.mp3` |
| WAV | `.wav` |
| Ogg Vorbis | `.ogg` |

**Features**

- **Playlist**: every track in the folder, sorted by name; click any row to play it
- **Transport**: previous / play-pause / next / stop, plus a **loop** toggle
- **Volume slider** styled like the rest of the mod's menu (0–100%)
- **Refresh button** (top-right of the window): re-scans the folder, so you can drop new files in **while the game is running** and have them show up immediately
- **Remembers where you left it**: both the **window position** (drag the title bar) and the **volume** are saved and restored on the next launch
- **Auto-advance** to the next track when one finishes (with loop off)
- **"Now playing" line** at the top of the window, always showing **`Now playing <track name>`** (or `Paused <track name>`), so you can see what is playing at a glance
- **Mutes the game's own music while the player is playing** — both the lobby music and the main-menu music are silenced so they do not fight with your track. They come back as soon as you pause, stop, or turn the volume to zero. Independent toggle: **Settings → Lobby & room list → Mute game music while playing** (`Music.MuteGameMusic`, on by default). Ambient sound effects are deliberately **not** touched
- Decoding happens on a background thread, so starting a large track does not freeze the game
- Tracks longer than **20 minutes** are rejected (decoded audio is held in memory; this bounds it)

**Notes**

- The player is a **local, client-side** feature: only you hear the music, and nothing is sent over the network
- The music player decodes audio with two bundled third-party open-source libraries (**NAudio** and **NVorbis**, both MIT). They are embedded inside `Amethyst.dll` and extracted to `Amethyst\libs\` on first use, so **no extra downloads are needed**. Their license texts are placed in that same folder — see `THIRD-PARTY-NOTICES.txt`
- If a track fails to load, the window shows `Load failed`; check `BepInEx\LogOutput.log` for a `[Music]` line with the reason

### 🏆 Winner Role Reveal

- On the post-match screen, the **winning players' roles** are shown **under their names** on the result avatars
- The winning lineup comes from the game's own cached winners, and roles are recovered from the mod's in-match snapshot, so it works across the end-screen scene change
- Toggleable independently under **Other Features → UI Optimization**

### 🎮 Role Colors

Every **vanilla role** is rendered in its own color instead of the flat impostor-red / crewmate-blue.

| Role | Color | | Role | Color |
|---|---|---|---|---|
| Crewmate | `#8CFFFF` | | Impostor | `#C61111` |
| Engineer | `#FF6A00` | | Shapeshifter | `#C61111` |
| Scientist | `#8EE98E` | | Phantom | `#C61111` |
| Guardian Angel | `#77E6D1` | | Viper | `#C61111` |
| Tracker | `#34AD50` | | Impostor Ghost | `#C61111` |
| **Noisemaker** | `#FF4A62` | | Crewmate Ghost | `#8CFFFF` |
| Detective | `#625EEE` | | Judge | `#F8D85A` |
| Spirit Guide (Influencer) | `#FF0066` | | | |

- Applied to the **role-assignment screen** (the intro "You Are" text), the **overhead role name**, meetings, chat, and the role-info panel
- Any role added by a future game update automatically falls back to its team color, and a startup self-check reports any role that has no dedicated color

### 📖 Role Info Panel

An in-game panel (a second task-like panel you can click to collapse/expand) showing your role and its description.

- Each of the 15 vanilla roles has a **short flavour line** and a **detailed rules text**, **fully localized in Chinese, English and Russian**
- Ghost-role detection uses the game's own `RoleManager.IsGhostRole`, so roles added by future game updates are picked up automatically
- While alive as a crewmate it only shows your own role; after you die (or as an impostor) it also lists the other players you are allowed to see

### 🎨 UI Optimization

> The main menu's right-panel slide-in animation is built in and **always on**.

**Note: there is no longer an "UI Optimization" master switch** — every feature in this group has its **own independent toggle**,
set individually under **Other Features → UI Optimization**:

- **Main menu optimization**: trims the clutter (background noise, left panel fill and dividers, window highlights, full-screen tint, friend-request badge) while **keeping the Among Us logo**
- **Main menu slide-in**: clicking "Play / My Account / Credits" slides the right panel in from the right; opening Settings, Announcements, Inventory or Shop slides it out so it no longer covers the popup
- **Main menu link buttons**: three buttons at the bottom-left of the main menu — **GitHub**, **QQ Group** and **Report Cheater** — all built from the same template so they look identical. **Report Cheater** opens the cheat-report page **`api2.elauk.top/acban/submit`** in your browser, copies the link to the clipboard as a fallback and shows a toast. All three are hidden together with the `Other.MainMenuButtons` toggle
- **Meeting role tag**: in a meeting each player has an `EditTag` icon next to their avatar. Click it to open the game's own shapeshifter menu and mark that player with a role;
  the mark appears **next to the meeting name** and **above the player's head in game**. Impostors are listed first in red, crewmates after them in blue, with a "clear tag" entry at the end.
  All tags are cleared automatically when the match ends (local only — only you can see them; your own slot shows no icon)
- **Recap info**: press `F2` in the lobby to review the previous match (see "Recap Info" above)
- **Task panel in meetings**: keeps your task list visible during meetings, raised above the meeting UI
- Force-show the start button, skip the kill animation
- Display improvements: better ping / cooldown display, sabotage cooldown display
- **Better ping display**: an in-game readout with **selectable items** — latency, FPS, server/region, **room code + (players/max players)** and **host name**. Turn each one on or off under **Other Features → Cooldown & Ping Display**; unselected items are simply not shown
- **Server display**: shows which region/server you are on, localized per language (e.g. "Asia" / 「亚洲」 / «Азия»), with no abbreviations
- **Room info**: Find Game lists up to 10 lobbies in a scrollable list (scrolls **one whole row at a time**, so rows can never be drawn outside the list area), each row showing host name / platform / room code
- **Color name display**: shows each player's color name near them
- **Name colors**: your own and other players' names are rendered in their color; from an impostor's perspective teammates are shown in red (applies to chat bubbles, meeting votes and the "who died / who reported" intro screen; the ejection result screen keeps the game's native look)
- Display options: show the host in the top-left of a meeting, show your own role, show impostor teammates' roles, show everyone's role after you die
- **Outfit saving**: 6 preset buttons on the inventory page for one-click outfit switching
- Unlock 240 FPS, unlock all cosmetics, skip the disconnect penalty and auto-return to lobby each have their **own independent toggle** under "Display & Roles" (**also independent — they do not follow any other switch**)
- **Better chat**: rich text input, copy/paste, bubble animation, dark theme
- Floating menu button

### ⚡ Performance

All under **Other Features → Performance**, all **on by default** (turn them off if you prefer):

- **Don't update dead players**: skips most `FixedUpdate` frames for **dead** players. This is the single biggest win in a full lobby.
  A skip-count slider (2–300, default 60) controls how aggressively this is applied. Slight trade-off: dead players' overlays refresh a little slower.
- **Low load mode**: round-robin player updates — only one player is fully updated per frame while the rest are skipped. Only engages above 8 players; the local player and the host are never skipped.
- **Hide console window**: hides the black BepInEx console window at startup.
- A **per-second update scheduler** distributes the mod's own periodic work across frames instead of running it all in one frame, which removes the periodic frame-time spikes.

### 🚀 Startup & Update Check

- **Update check on startup**: the mod checks for a newer version in the background and, if one is available, shows an "update available" hint in the mod menu. The check never blocks or delays the game's normal startup.

### 🔍 Mod Client Detection

- Detects and marks other compatible mod clients in the room

### 🎨 Personalization

- Languages: Chinese / English / Русский
- Adjustable UI theme and scale; telemetry/crash reporting can be disabled

---

## Hotkeys

| Key | Action |
|:---:|:-------|
| `Insert` | Open / close the main menu |
| `F2` | Show / hide recap info (lobby and post-match) |
| `F6` | Copy the current lobby code |
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
5. Put **`Amethyst_v1.1.5.dll`** into `BepInEx/plugins/`
6. Launch the game and press `Insert` to open the menu

> The first launch creates the config files under `BepInEx/config/`; delete the matching `.cfg` to reset your settings.

---

## Anti-Cheat Notices

An anti-cheat hit raises a notification and applies the configured action (warn/kick/ban). Common detections include, but are not limited to:

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
| Venting on another player's behalf | Forcing another player into/out of a vent |
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

The build output is `src/bin/Release/netcoreapp6.0/Amethyst.dll` (plus a versioned copy `Amethyst_v1.1.5.dll`).

---

## Changelog

### v1.1.5

- **New — built-in music player** (`M`): plays your own audio files from `Amethyst\Music\` while you play, supporting **MP3 / WAV / OGG**. Click any row to play, with previous / play-pause / next / stop, a loop toggle and a volume slider. Decoding runs on a background thread so large tracks do not freeze the game. The window shows the **folder path at all times**, has a **refresh button** (drop new files in while the game is running and they appear immediately), always displays **`Now playing <track>`** at the top, and **remembers its position and volume** between launches.
  - **Also new — the game's own music is muted while the player is playing.** Lobby music and main-menu music are both silenced so they do not fight with your track, and restored as soon as you pause or stop. Its own toggle lives under **Settings → Lobby & room list → Mute game music while playing** (`Music.MuteGameMusic`, on by default). Ambient sound effects are deliberately left alone.
  - **New hotkey**: `Settings → Hotkeys` now has a **music player** row, so the open/close key is rebindable like the others (`Keys.MusicKey`, default `M`). While the window is open, `[` / `]` change track and `P` plays/pauses.
- **New — `Mode` and `Language` in the copied room info** (`F6`). The wording follows the **mod's** language, but the **values are read from the room**: `Mode` from the room's game mode (Classic / Hide & Seek) and `Language` from the room's own language setting.
- **Fixed — recap panel is no longer described as draggable**: it slides in and is docked on the left; dragging was removed in an earlier round and the docs still said otherwise.
- **Changed — recap panel and music player text is now bold**, and the music player's "now playing" line uses a larger, accented style so it stands out as the window's main heading.
- **Fixed — the music player's "now playing" line showed no track name while playing.** Starting a track used to set a transient status message, and a transient status replaces the whole line — so while playing you only saw `Now playing`, and the track name appeared only after pausing (once the transient had expired). Starting a track no longer sets one; the line is now derived from state every frame, so it always reads `Now playing <track>` / `Paused <track>`.
- **Fixed — the music player could not actually play anything.** The first implementation used `UnityWebRequestMultimedia.GetAudioClip`, but the game's IL2CPP build has **stripped** `DownloadHandlerAudioClip`'s constructor, so the call threw `MissingMethodException` at runtime and no `AudioClip` was ever produced (it looked like a very long load followed by silence). Audio is now decoded in managed code and handed to Unity via a streaming `AudioClip`. This bundles two MIT-licensed libraries (**NAudio** for MP3/WAV and **NVorbis** for OGG) **inside `Amethyst.dll`** — they are extracted to `Amethyst\libs\` on first use, so nothing extra has to be downloaded. Their license texts ship in that folder (`THIRD-PARTY-NOTICES.txt`), as the MIT license requires.
- **Fixed — ghost names strobing in Hide & Seek (hide-names on)**: after you die as a Crewmate Ghost, the mod forces every player's name to stay visible (Hide & Seek hides crewmate names). That assertion used to run only every 10th frame while the game re-hides those names **every** frame, so the names the game hides were re-shown for a single frame at 6 Hz — the "some ghost names flicker, some stay visible" report. The assertion is now made every frame, but **only inside Hide & Seek** (and only while the local player is dead), so classic matches and every alive frame keep the previous, cheaper cadence. Ghost names are now steady instead of strobing.
- Version 1.1.4 → **1.1.5**

### v1.1.4

- **New — Report Cheater button on the main menu**: a third link button now sits to the **right of the QQ Group button** (bottom-left of the main menu), built from the same template as the GitHub and QQ Group buttons so all three look identical. Clicking it opens the cheat-report page **`api2.elauk.top/acban/submit`** in your browser, copies the link to the clipboard as a fallback and shows a toast. Its label is fully localized (中文 / English / Русский) and it follows the same `Other.MainMenuButtons` toggle as the other two.
- **Removed — three protections.** The patches, their config entries, their menu switches and their localization keys are all gone:
  - **Block server-forced position updates** (`Guard.BlockServerTeleports`) — the `SnapTo` RPC aimed at your own client is no longer dropped.
  - **Hardened PackedUInt parsing** (`Guard.HardenPackedUInt`) — the mod no longer substitutes its own `MessageReader.ReadPackedUInt32`; the game's own implementation is used for every read again.
  - **Force DTLS connection** (`Guard.ForceDtls`) — the client no longer forces `InnerNetClient.SetEndpoint(dtls: true)`.
  - Untouched: the other two network-protection toggles (oversized GameData packets, invalid GameData type), the VotingComplete overload guard, and the vent/zipline protections.
- **Cleanup**: the card heights of the affected settings cards were recomputed so no empty space is left where the removed rows used to be.
- > Upgrading? The three removed keys stay behind in an existing `BepInEx/config/amethyst.mod.cfg` as orphans. They are ignored and safe to delete by hand.
- Version 1.1.3 → **1.1.4**

### v1.1.3

- **Anti-Cheat: Host TempBanAll Exploit Protection & Detection**:
  - Added dedicated protection against host `TempBanAll` exploits (spamming `StartGame` packets within 0.05s).
  - Split into two independent toggles under the anti-cheat tab:
    - **Prevent host temporary account ban exploit** (`BypassDisconnectPenalty`): Automatically suppresses rapid malicious `CoStartGame` coroutines and clears disconnect ban points (`PlayerBanData.BanPoints = 0f`), keeping the client in the room without lag or temporary ban penalty. Default: ON.
    - **Host temporary account ban exploit detection** (`DetectHostTempBan`): Alerts the player with a dedicated high-priority warning toast when an attack occurs. Default: ON.
  - Exploit toast notification duration extended to **10 seconds** to ensure high visibility.
- **Unified Toast Notification Durations**:
  - Regular toasts (clipboard copy, room code copy, ban import, etc.) standardized to **3.0 seconds**.
  - All standard anti-cheat alerts (impossible RPC, flood detection, mod client detection, out-of-bounds teleport, early meetings, etc.) standardized to **5.0 seconds**.
  - Toast display duration limit widened from 8s to 15s to support longer-duration security notices.
- **Removed Feature**:
  - Removed "Force impostors below minimum players" (`ForceImpostorLowCount`) and related patches (`AmethystForceImpostorSelectPatch`, `AmethystHnsForceSeekerPatch`) for cleaner role management and game stability.
- **UI & Layout Optimizations**:
  - Re-anchored anti-cheat card heights and dynamic expansion logic to prevent card overflow, text clipping, and unclickable toggles.

### v1.1.2

- **Main Menu Link Buttons (EHR Style)**: Added GitHub and QQ Group link buttons on the main menu styled after EndlessHostRoles 8.0.2 (`MainMenuManagerPatch`), complete with custom colors, hover states, clipboard copying, browser open, and toast notifications.
- **Main Menu Links Toggle**: Added an independent switch under `Visuals / Main Menu` (`MainMenuButtons`) to toggle the main menu GitHub and QQ Group buttons on or off at any time.
- **Enhanced F6 Room Info Copy**: Optimized F6 shortcut to copy multi-line room information formatted as:
  ```
  Room Code + (Current/Max Players)
  Host: HostName
  Server: ServerName
  ```
- **Multilingual Support for Server & Main Menu**: Expanded 3-language localizations (`zh_CN`, `en_US`, `ru_RU`) for main menu buttons, toast notifications, and dynamic server region names in the ping display.

### v1.1.1

- **Fixed — names and colour-blind text losing their colour in the lobby**: after the game rebuilt a player's text, the mod still believed it had already written the colour and never re-applied it, so many players' names and colour names showed up plain. The write path now compares the text that is actually on screen, matching the reference implementation.
- **New — winner role reveal on the post-match screen**: the winning players' roles are shown under their names on the result avatars (toggleable under **Other Features → UI Optimization**).
- **Changed — recap window**: it no longer opens automatically. A **Show/Hide recap info** button in the top-left corner toggles it, and it is available both in the lobby and on the post-match screen; `F2` still works.
- **Fixed — dead players kept their overhead role tag**: the alive role label was still generated for a player after they died, so it stayed above their body; it is now cleared on death.
- **New — your own name is always visible, and everyone's after you die**: the local player's name is force-shown even in modes that hide names (e.g. Hide & Seek). Once you are dead, every player's name is force-shown too; before that, other players' names still follow the game's setting.
- **New — Developer: role assignment algorithm**: a cycle option that reshuffles the player list before the game assigns roles, using the selected random algorithm (Default / System / Xorshift / Mersenne Twister). Host only; Default leaves the game unchanged.
- **New — Developer: impostors below the minimum player count**: when enabled, after the game's own role assignment the host checks whether an impostor exists and, if not, promotes a random player to impostor. The vanilla assignment still runs (so the intro is not delayed), and it works in Hide & Seek as well.
- **New — role assignment algorithm applies to Hide & Seek too**: the chosen random algorithm also shuffles the Hide & Seek role assignment.
- **Fixed — Hide & Seek seeker had no transformation**: the extra seeker is now assigned during the game's own role-selection step (like the reference mod), so the normal seeker animation and form play.
- **Fixed — impostors moved without animation on other screens**: the guard dropped impostors' `PlayAnimation` RPCs, so on maps like The Fungle other players saw an impostor riding a zipline as a plain sliding/teleporting figure. The check is removed (the reference mod's `PlayAnimation` handler is empty too).
- **Fixed — winner roles showing under the wrong player**: the end-screen avatars were matched before the game had written their names (they were all `???`), so roles could be attached to the wrong person. The reveal now waits until the names are ready and matches by name, falling back to order only if needed.
- **Fixed — safer extra impostor**: a player who disconnected while loading is never promoted, avoiding a black screen.
- **Changed — ping display room/host**: in a local game the room shows **Local** instead of `?`; the room code uses the theme colour, the player ratio is green when more than one player is present and red otherwise, and the host name is drawn in the host's own colour.
- **Changed — colour snipe UI**: the colour chooser now has **◂ / ▸** arrows so you can step forward and back instead of only cycling forward and overshooting.
- **Changed — in-game role info panel**: hidden in Hide & Seek (where it is not useful) and shown normally in Classic.
- **Changed — main menu**: the version line now reads **platform · version · update date · ♥ Amethyst v… by xiaozi ♥** with a left-to-right shine on the mod name; its scale is fixed to 1 and its position is left to the game.
- **Fixed — Find Game buttons**: the Refresh and Back buttons used a mix of the original blue and the theme purple; their background, hover, selected and text colours are now consistent.
- **Fixed — incomplete room info in Find Game**: per-row info is now written on `GameContainer.SetupGameInfo` (like the reference mods), so every room shows the host name / platform / room code, with `Unknown` fallbacks for missing data.
- **Fixed — tearing / stutter at 60 FPS**: when the FPS unlock is off, vertical sync is now enabled (it had been forced off), which removes the tearing at 60 Hz; enabling the FPS unlock still turns v-sync off so higher caps work.
- **Fixed — main menu friend-request badge**: the background, number and highlight were all tinted the same colour; they now use a purple background, white number and a soft highlight.
- **Changed — main menu art**: the displayed image was replaced.
- **New — compatibility handling**: the mod now detects a **duplicate Amethyst assembly** (e.g. an old hotfix DLL next to the current one) and warns you to keep only one; it swallows the repeated `NullReferenceException` from ModExplorer's `ModManager.LateUpdate` (which otherwise spams the log and can crash); and when a same-purpose plugin is installed (BetterPingDisplay / BetterCooldownDisplay / Sabotage Cooldown Display / OutfitSaver / ForceShowStart / Unlock All Skins / FPS & Network Optimizer) the corresponding Amethyst feature steps aside so the two do not fight.
- Version 1.1.0 → **1.1.1**

### v1.1.0

- **New — Influencer / Spirit Guide support**: the crewmate ghost role added in game v19.0 is now fully covered, with its own colour, localized name, flavour line and rules text. It is correctly excluded from the role-tag picker and from the "see everyone's role after death" list. Ghost roles are detected from the game itself, so roles added by future game updates are picked up automatically.
- **Fixed — a crash when opening the meeting role tag**: opening the role-tag picker during a meeting could throw a null reference inside the meeting UI and crash the game. The picker is now stable.
- **Fixed — Hide & Seek protections did not actually drop the RPC**: reporting a body, calling a meeting and a seeker venting only raised a notice; they now drop the packet like every other detection.
- **Fixed — Hide & Seek sabotage could be bypassed**: sabotage is now blocked regardless of who the packet claims the actor is.
- **Fixed — the role info panel overlapped the task panel** in Hide & Seek and while playing an impostor. Task-mode detection now uses the game mode, and in H&S the panel is moved further down.
- **Fixed — your role was shown twice**: the duplicate role block in the task panel is removed.
- **Removed — task text recolouring**: the task panel now uses the game's own colours. Only role-name colouring remains.
- **Removed — the "Host" tab**: replaced by a **Developer** tab holding the performance probe and no-win-conditions.
- **Fixed — no-win-conditions did nothing in some game modes**: it now blocks all three end-of-game paths (normal, Hide & Seek and the "all tasks completed" win).
- **Fixed — role descriptions**: the Crewmate and Judge texts were corrected.
- **Removed — startup splash screen and rebranding**: the game now starts normally, and the update check runs quietly in the background.
- Credits: **海王星Neptune** added to the donor list.
- Version 1.0.9 → **1.1.0**

### v1.0.9

- **New — Role colors**: every vanilla role is now rendered in its own color, applied to the intro role-assignment screen, the overhead role name, meetings, chat and the role-info panel. A startup self-check reports any role that has no dedicated color.
- **New — Role info panel rewritten**: the panel now shows your role plus its **short flavour line and detailed rules**, and it no longer jitters. It is created once and fully owns its own `Update` (the previous version fought the game's own position writes every frame, which caused the trembling).
- **New — Per-role descriptions**: all 14 vanilla roles have a flavour line and a rules text, **fully localized in Chinese, English and Russian**. A startup self-check verifies all 3 languages × 14 roles are present.
- **New — Task text color**: unfinished tasks are shown in yellow and finished ones in green instead of the game's dim grey.
- **New — Task panel in meetings**: your task list stays visible during meetings, raised above the meeting UI.
- **Performance rewrite — per-player work is now staggered**: this is the fix for the mod feeling heavy, and for the performance toggles appearing to do nothing. Previously five or six components each walked every player **every frame** in `LateUpdate`, reading native fields (`pc.Data.PlayerName`, `pc.cosmetics.nameText`, `shapeshiftTargetPlayerId`, `CurrentOutfit`, …) and — worst of all — reading `TMP.text`, which **allocates a managed string on every read**. A 10-player lobby meant dozens of cross-IL2CPP calls and dozens of string allocations per frame, so the GC spiked every few seconds.
  There is now a single **player snapshot cache** refreshed every frame for global state and **staggered per player over 20 frames** (only ~n/20 players are read on any given frame). Name colours, colour-blind names, overhead role tags, meeting role text and mod-client tags all consume that snapshot, so their per-frame loops are pure managed comparisons that write to TMP only when a value actually changes.
  - Overhead role tags no longer rebuild their rich text or read `TMP.text` every frame; the text is built in the staggered pass.
  - Meeting role text no longer re-runs `FindPlayer` + task iteration for every panel every 5 frames.
  - Mushroom-mixup name hiding no longer rescans every player every frame while the sabotage is active.
  - The meeting role-tag suffix is cached per player instead of being re-formatted on every read.
- **New — Performance section** (all on by default):
  - **Don't update dead players** — skips most `FixedUpdate` frames for dead players (adjustable skip count, default 60)
  - **Low load mode** — round-robin player updates, engaging above 8 players
  - **Hide console window** — hides the black BepInEx console at startup
  - **Per-second update scheduler** — spreads the mod's periodic work across frames to remove periodic frame-time spikes
- **New — Server display**: the better ping display is now a three-part readout — **latency | FPS | server** — with the region name shown in full and localized per language, with no abbreviations.
- **Fixed — Role info panel position** and the panel being invisible (the background scale was computed from stale/empty text bounds).
- **Fixed — Intro role colors not appearing**: the patch now hooks the `ShowRole` coroutine's state machine (`_ShowRole_d__41.MoveNext`) instead of `CoBegin`/`CoShowIntro`, which the game never calls from managed code.
- **Fixed — Raw rich-text markup leaking** (e.g. `<color=##FF4A62>` shown as literal text) caused by a double `#` in the colour value.
- **Fixed — Task text colour patch silently failing to attach**, and a false "role has no colour" warning.
- **Fixed — FPS counter reading ~3**: the frame counter had been placed behind a per-second throttle, so it sampled once per second instead of every frame.
- **Fixed — Overlapping text** in the server readout caused by applying `<mspace>` (a monospace tag) to wide CJK glyphs.
- **Fixed — Role info panel showing only one role** and no content: the body now shows your role and its description, with other players listed only when you are allowed to see them.
- **Fixed — Localization loader**: text lookup is now case-insensitive, so `MENU`/`Menu`, `BAN`/`Ban` and `IMPORTTXT`/`ImportTxt` all resolve instead of sometimes showing the raw key.
- **Fixed — Recap window auto-opening on lobby entry**: the window's visibility flag defaulted to `true`, so it popped up the moment you entered a lobby. It now **starts closed** and only opens when you press the recap hotkey (`F2`), except for the single automatic open on the post-match screen — which is edge-triggered, so closing it there keeps it closed.
- Actions on the role-info panel, the UI-optimization toggles and the performance toggles now **default to on**; turn them off manually if you prefer.
- Version 1.0.8 → **1.0.9**

### v1.0.8

- Splash screen: now checks for a mod update first (network error / outdated / up to date, switched with a fade) and only enters the main menu once the check completes; "Starting game" with a spinner is always shown in the bottom-right; custom wordmark and the purple crewmate are not applicable.
- Splash screen text: during loading the bottom-right shows "Setting up your game" with a spinner.
- UI optimization: added a "hover buttons in theme color" toggle (applies globally to every interactive button).
- Loading screen: the vanilla Among Us logo is replaced with the mod's wordmark; the loading bar keeps its vanilla colors.
- Main menu optimization: hides the friend-list backdrop; theme switching removed and fixed to #A06EFF.
- Role tag: fixed the "panel hidden behind meeting nameplates after a death" issue (nameplates are temporarily hidden while the panel is open and restored on close).

### v1.0.7

- **New — Home info**: a dedicated **"More info"** card on the home page (**System** (incl. architecture) / **Game version** / **BepInEx version** / **loaded mod count**); the "About" card keeps only the mod's own version and author
- **Mod notifications are now independent**: anti-cheat / protection / management / mod-detection notifications use the **mod's own notification card** (top of the screen) instead of the game's bottom-left notification area
- **New — startup loading screen**: takes over the loading screen with the mod's wordmark (hand-drawn icon) + breathing glow + version in the centre, and "Starting game" with a spinner ring in the bottom-right
- **Notification polish**: plain-text cards (**no player colours rendered**), fading in and out in sync with the card; no player cosmetics shown
- **Removed "show platform and level"**: the mod no longer posts its own join/leave notices; normal join/leave is handled entirely by the game's own notifications
- **FPS unlock is now adjustable**: the switch became "Unlock FPS", and once enabled you can slide (or use the arrows) to pick a cap between **60 and 240**, default 240; turning it off returns to the vanilla 60
- **Theme fixed**: the theme picker was removed and the UI theme is forced to **#A06EFF** (previously listed as Violet)
- **New — main menu background image**: fills the main menu with your own image (aspect-fill, resolution-aware) and hides the vanilla floating characters and dots at the same time
- **New — snow effect**: snow falls on the main menu, independently toggleable; it does not scale or cover buttons and is brighter
- **New — ability duration decimals**: besides cooldowns, the ability **duration** is now shown with one decimal place
- **New — cheat RPC detection**: recognises RPC signatures of known cheat menus and folds them into the anti-cheat (notify-only by default)
- **Fixed — name rendering**:
  - the overhead name being wiped out and miscoloured while a shapeshifter is shifted
  - names in the shapeshifter menu not being coloured by player colour
  - **the local player's own** name turning red after shifting (name colour now follows the body colour)
- **Fixed — mushroom mixup sabotage**: names were not hidden and name colours were not rendered during the sabotage in freeplay; names are now hidden during it and restored afterwards
- **Fixed — map loading progress as a room member**: the bar used to sit at 30% ("Generating map") forever because the game only reports real progress on the host; it now advances smoothly to about 68% and waits for the map to be ready
- **Fixed — main menu slide-in animation**: no animation when clicking "Credits" first and then "Play"; also removed a per-frame `GameObject.Find` that caused frame drops
- **Fixed — meeting role tag panel**: hidden behind meeting nameplates, most slots missing their character, semi-transparent border; the panel size is now 1.1×
- **Fixed — latency / FPS display**: raised to the top layer, position and letter spacing adjusted
- **Removed "other mod detection"** (stability proved insufficient after testing, removed entirely as requested)
- Version 1.0.6 → **1.0.8**

### v1.0.6

- **New — meeting role tag**: each player has an EditTag icon next to their avatar in a meeting; click it to open the game's own shapeshifter menu, pick a role and mark that player;
  the mark shows next to the meeting name and above the player's head in game, **impostors red / crewmates blue**, impostors listed first with crewmates after; a "clear tag" entry is at the end of the list and all tags are cleared when the match ends
- **New — player history / cheat history**: always on, written to file only and never shown in any UI.
  Records each player's name / friend code / PUID / platform / level and supports **reading history back** by friend code
- **New — recap info**: in the lobby you can review each player's role, kills / task progress, cause of death and the match result from the previous game
- **Fixed — blank "Settings" page in the mod menu**: a sub-tab array index went out of bounds, so the whole page rendered nothing
- **Fixed — several recap issues**: disconnected players shown as "alive", the winning side sometimes locked to the wrong one, and the role lookup cache holding an empty table
- **Fixed — players who left abruptly showing as TESTNAME**
- **Fixed — main menu optimization**: the hidden effects were unintentionally restored after opening Settings
- Removed the experimental "settings page colours" feature (tested and results were unsatisfactory)
- Version 1.0.5 → **1.0.6**

### v1.0.5

- **New — main menu optimization** (merged into the "UI optimization" master switch): trims the main menu clutter while keeping the Among Us logo
- **New — main menu slide-in animation**: clicking "Play / My Account / Credits" slides the right panel in from the right;
  opening Settings, Announcements, Inventory or Shop slides it out so it no longer covers the popup
- **Fixed — the main menu "Settings" button not opening**: the vanilla `OptionsMenuBehaviour.Open()` threw a null reference so the settings popup never appeared; fields are now filled in with a fallback
- **Fixed — meeting intro name colours**: fixed the reporter's and the victim's names not being rendered in player colour on the "who died / who reported" intro screen;
  the ejection (exile result) screen is no longer recoloured and keeps the game's native look
- **Outfit saving / one-click switching**: 6 preset buttons on the inventory page
- Version 1.0.4 → **1.0.5**

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
