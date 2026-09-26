<div align="center">

# Amethyst v1.0.9

**An Among Us anti-cheat and utility mod**

A BepInEx (IL2CPP) client-side mod for Among Us that provides RPC anti-cheat, player management, network protection and a broad set of in-game/lobby quality-of-life features. Intended for hosts who want to keep cheaters out of their own private lobbies.

<br>

<img src="https://img.shields.io/badge/Among%20Us-IL2CPP-000000?style=for-the-badge&logo=amongus&logoColor=white" alt="Among Us">
<img src="https://img.shields.io/badge/BepInEx-6.x-5865F2?style=for-the-badge" alt="BepInEx">
<img src="https://img.shields.io/badge/.NET-6.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET">
<img src="https://img.shields.io/badge/version-1.0.9-9b59b6?style=for-the-badge" alt="version">
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
- **Anti-cheat server bypass**: uses DTLS to bypass the server-side RPC anti-cheat for non-host actions, making local blocking far more reliable

### 🕵 Player Management

- **Join/leave detection**: shows a joining player's nickname, platform and level
- **Ban list**: kick/ban on join by FriendCode / PUID
- **Level guard**: set a minimum / maximum level and choose an action
- **Player history / cheat history** (**always on**, written to file only and never shown in any UI):
  every join/leave records name / friend code / PUID / platform / level to `Amethyst/PlayerHistory.txt`;
  every anti-cheat hit records name / friend code / PUID / platform / reason to `Amethyst/CheatHistory.txt` (same folder as the ban list).
  Supports **reading history back** by friend code, which is used to recover the names of players who left abruptly

### 🌊 Network Protection

- Drops forced position teleports aimed at your client
- Drops oversized GameData packets to prevent large-message crashes/overload
- Drops GameData sub-messages with an invalid/malformed type
- Hardens PackedUInt deserialization against malformed fixed-length integers
- Guards VotingComplete against an oversized voter array (allocation overload)
- **Spawn flood protection**: trims a flood of spawns in a single frame to prevent freezes/crashes

### 🧩 Hide & Seek Protection

- Detects and blocks abnormal reports, door closes, sabotage and illegal venting in H&S

### 👥 Room & Session

- **Show host in meetings**: shows the host in the top-left corner during a meeting
- **Role display**: shows everyone's role after you die; while alive you can show your own role and your impostor teammates' roles
- **Kick/ban during a match**: the host can kick/ban mid-match
- **Color snipe**: lobby only, automatically claims your desired color when it becomes free (optional, off by default)
- **Auto return to lobby** and **NoWin** (never end the match)

### 📋 Recap Info

- Press `F2` in the lobby to open a draggable **recap window** showing, for every player in the **previous match**:
  role (alive `=>` dead), kill count / task progress, cause of death (killed with the killer listed / ejected / disconnected / died / survived) and the match result
- One-click copy as plain text
- **Lobby only** — showing other players' roles during a match would be cheating, so it is never displayed in-game
- **Open behavior**: the window is **closed by default** and only opens when you press `F2`, except that it opens **automatically once on the post-match screen**. The open/closed state is not saved, so entering a lobby always starts closed. Closing it on the post-match screen keeps it closed.

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

- The palette is ported from [EndlessHostRoles](https://github.com/Gurge44/EndlessHostRoles) (`RoleHtmlColors`, vanilla section)
- Applied to the **role-assignment screen** (the intro "You Are" text), the **overhead role name**, meetings, chat, and the role-info panel
- Any role added by a future game update automatically falls back to its team color, and a startup self-check reports any role that has no dedicated color

### 📖 Role Info Panel

An in-game panel (a second task-like panel you can click to collapse/expand) showing your role and its description.

- Each of the 14 vanilla roles has a **short flavour line** and a **detailed rules text**, ported from EndlessHostRoles and **fully localized in Chinese, English and Russian**
- While alive as a crewmate it only shows your own role; after you die (or as an impostor) it also lists the other players you are allowed to see

### 🎨 UI Optimization

> The main menu's right-panel slide-in animation is built in and **always on**.

**Note: there is no longer an "UI Optimization" master switch** — every feature in this group has its **own independent toggle**,
set individually under **Other Features → UI Optimization**:

- **Main menu optimization**: trims the clutter (background noise, left panel fill and dividers, window highlights, full-screen tint, friend-request badge) while **keeping the Among Us logo**
- **Main menu slide-in**: clicking "Play / My Account / Credits" slides the right panel in from the right; opening Settings, Announcements, Inventory or Shop slides it out so it no longer covers the popup
- **Meeting role tag**: in a meeting each player has an `EditTag` icon next to their avatar. Click it to open the game's own shapeshifter menu and mark that player with a role;
  the mark appears **next to the meeting name** and **above the player's head in game**. Impostors are listed first in red, crewmates after them in blue, with a "clear tag" entry at the end.
  All tags are cleared automatically when the match ends (local only — only you can see them; your own slot shows no icon)
- **Recap info**: press `F2` in the lobby to review the previous match (see "Recap Info" above)
- **Task text color**: unfinished tasks are shown in **yellow** and finished ones in **green** instead of the game's dim grey (reference: EndlessHostRoles)
- **Task panel in meetings**: keeps your task list visible during meetings, raised above the meeting UI (reference: AUnlocker)
- Force-show the start button, skip the kill animation
- Display improvements: better ping / cooldown display, sabotage cooldown display
- **Better ping display**: a three-part readout — **latency | FPS | server**
- **Server display**: shows which region/server you are on, localized per language (e.g. "Asia" / 「亚洲」 / «Азия»), with no abbreviations
- **Room info**: Find Game lists up to 10 lobbies in a scrollable list, each row showing host name / platform / room code
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

### 🚀 Splash Screen & Rebranding

- **Update check on startup**: the splash screen first checks for a mod update, showing "Checking for mod updates";
  there are three outcomes — network error (amber), outdated (red), up to date (green) — switched with a **fade**; the main menu is only entered **after the check completes** (with a 12-second hard timeout so it can never hang)
- **Splash screen**: replaced with the mod's own wordmark plus "Starting game" and a spinner in the bottom-right; the vanilla startup sound is muted
- **Loading hint**: during match loading (host and client alike) the bottom-right shows "Setting up your game" with a spinner
- **Rebranding**: the vanilla Among Us logos (main menu / loading screen / splash animation) are hidden and replaced with the mod's wordmark

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
| `F2` | Show / hide recap info (lobby only) |
| `F6` | Copy the current lobby code |

> Hotkeys can be changed in the `BepInEx/config/` file (`Keys.MenuKey` / `Keys.RecapKey` / `Keys.CopyCodeKey`).

---

## Installation

> Amethyst requires **BepInEx (IL2CPP, Windows x64)**.

1. Download and install **BepInEx BleedingEdge — IL2CPP (`win-x64`)**
2. Locate your Among Us install folder:
   - **Steam**: Library → right-click Among Us → Manage → Browse local files
   - **Epic**: Library → Among Us → Manage, usually `C:\Program Files\Epic Games\AmongUs`
3. Extract BepInEx into the game folder (next to `Among Us.exe`)
4. Launch the game once to the main menu, then close it (this creates the `BepInEx/plugins` folder)
5. Put **`Amethyst_v1.0.9.dll`** into `BepInEx/plugins/`
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
| Known cheat menu signatures | RPC signatures of known cheat menus |
| H&S report / sabotage / doors / illegal vent | Hide & Seek anomalies |
| Lobby game RPCs | Fake match RPCs (kills, meetings, shapeshifts) before the match starts |
| RPC flood | A single client spamming RPCs |

---

## Building From Source

You need Among Us assembly references (`Assembly-CSharp.dll` and friends). The project points at an extracted reference folder via `GameRefsDir`.

```
dotnet build src/Amethyst.csproj -c Release
```

The build output is `src/bin/Release/netcoreapp6.0/Amethyst.dll` (plus a versioned copy `Amethyst_v1.0.9.dll`).

On a **Release** build the DLL is also **deployed automatically** into the game's `BepInEx/plugins/` folder, so you can rebuild and simply restart the game. The target directory can be overridden:

```
dotnet build src/Amethyst.csproj -c Release -p:DeployDir="D:\Steam\...\BepInEx\plugins\"
```

Deployment is skipped (with a warning) if the directory is missing or the game is running, and it never fails the build. Your `config` files are never touched.

---

## Changelog

### v1.0.9

- **New — Role colors**: every vanilla role is now rendered in its own color (palette ported from EndlessHostRoles), applied to the intro role-assignment screen, the overhead role name, meetings, chat and the role-info panel. A startup self-check reports any role that has no dedicated color.
- **New — Role info panel rewritten**: the panel now shows your role plus its **short flavour line and detailed rules**, and it no longer jitters. It is created once and fully owns its own `Update` (the previous version fought the game's own position writes every frame, which caused the trembling).
- **New — Per-role descriptions**: all 14 vanilla roles have a flavour line and a rules text, **fully localized in Chinese, English and Russian**, ported from EndlessHostRoles. A startup self-check verifies all 3 languages × 14 roles are present.
- **New — Task text color**: unfinished tasks are shown in yellow and finished ones in green instead of the game's dim grey.
- **New — Task panel in meetings**: your task list stays visible during meetings, raised above the meeting UI (reference: AUnlocker).
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
- **New — cheat RPC detection** (reference: FinalSpectrum): recognises RPC signatures of known cheat menus and folds them into the anti-cheat (notify-only by default)
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
