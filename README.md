<div align="center">

# Amethyst v1.0.8

**Among Us Anti-Cheat & Utility Mod**

An Among Us client mod based on BepInEx (IL2CPP) that provides RPC anti-cheat, player management, security protection, and practical in-game/lobby utility features. Suitable for hosts to use against cheaters in their own private rooms.

<br>

<img src="https://img.shields.io/badge/Among%20Us-IL2CPP-000000?style=for-the-badge&logo=amongus&logoColor=white" alt="Among Us">
<img src="https://img.shields.io/badge/BepInEx-6.x-5865F2?style=for-the-badge" alt="BepInEx">
<img src="https://img.shields.io/badge/.NET-6.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET">
<img src="https://img.shields.io/badge/version-1.0.8-9b59b6?style=for-the-badge" alt="version">
<img src="https://img.shields.io/badge/License-GPL--3.0-blue?style=for-the-badge&logo=gnu&logoColor=white" alt="License">

</div>

---

## Introduction

Amethyst is an Among Us anti-cheat mod intended for use in private rooms. It determines whether a player is cheating by catching "impossible RPCs" (such as crewmate kills, crewmate sabotages, venting during meetings, venting on behalf of others, vent-kick exploits, etc.), and lets the host choose to **Warn / Kick / Ban**. It also integrates player join/leave detection, a ban list, level guard, and several gameplay experience improvements.

> This mod is intended only for hosts to use in their own private rooms. Please do not use it in public rooms to avoid affecting other players' experience.

---

## Features

### 🛡 Anti-Cheat

- **RPC Detection**: Catches impossible RPCs (crewmate kills, crewmate sabotages, unauthorized venting, forged vent IDs, venting/sabotaging/closing doors during meetings, sabotages outside the map, Shapeshifter/Phantom privilege abuse, etc.)
- **Kill Detection**: Impostor killing Impostor, dead players performing kills, repeatedly killing already-dead players
- **RPC Discarding**: The host discards flagged abnormal RPC packets so they don't take effect
- **Handling Options**: For cheaters, choose **None / Warn / Kick / Ban**
- **RPC Flood Detection**: Detects high-frequency RPC spamming from a single client
- **Early Meeting Interception**: Blocks meetings/reports within the first 15 seconds of a game
- **Lobby Fake Meeting Interception**: Prevents fake meeting UI in the lobby

### 🚪 Vent / Zipline Protection

- **Vent Rules**: Venting during meetings, unauthorized venting, forged vent IDs, venting on behalf of others
- **Anti-Force-Vent / Anti-Vent-Kick**: Blocks forced pass-through and ejections targeting the local client
- **Anti Vent-Kick Exploit**: The host punishes clients that send the exploit packet
- **Anti-Force-Zipline**: Blocks forced ziplines targeting the local client
- **Anti-Cheat Server Bypass**: Bypasses the server's RPC anti-cheat for non-host actions via DTLS, making client-side interception more reliable

### 🕵 Player Management

- **Join/Leave Detection**: Shows joining players' names, platforms, levels, etc.
- **Ban List**: Kick/ban on join by FriendCode / PUID
- **Level Guard**: Set minimum/maximum levels and choose an action
- **Player History / Cheat History** (**forced on**, file-only, never shown in any UI):
  Logs each player's join/leave as name / friend code / PUID / platform / level to `Amethyst/PlayerHistory.txt`;
  When anti-cheat triggers, separately logs name / friend code / PUID / platform / reason to `Amethyst/CheatHistory.txt` (same directory as the ban list).
  Supports **reading back history** by friend code, used to fill in names of players who force-quit

### 🌊 Network Protection

- Discards forced position teleports targeting the local client
- Discards oversized GameData packets to prevent large-message crashes/overload
- Discards illegal/malformed GameData sub-messages
- Hardens PackedUInt deserialization against malformed fixed-length integers
- Protects against allocation overload caused by oversized VotingComplete vote arrays
- **Spawn Flood Protection**: Trims massive single-frame spawns to prevent freezes/crashes

### 🧩 Hide & Seek Mode Protection

- Detects and blocks abnormal behaviors in H&S such as reports, door closing, sabotages, and illegal venting

### 👥 Room & Session

- **Show Host in Meetings**: Displays the host in the top-left during meetings
- **Role Display**: Shows all roles after death; while alive, can show your own role and Impostor teammates' roles
- **Unlock Kick/Ban During Matchmaking**: The host can kick/ban players during a match
- **Color Sniping**: Only works in the lobby; automatically snipes a target color when it's free (optional, off by default)
- **Auto Return to Lobby**, **NoWin (don't end the match)**

### 📋 Recap Info

- Press `F2` in the lobby to open a draggable **Recap Window** showing **last match's** details for each player:
  Role (alive `=>` dead), kills / task progress, cause of death (killed with killer noted / ejected / disconnected / dead / alive), and the match result
- Supports one-click copy as plain text
- **Lobby-only** — showing others' roles during a match is cheating, so it is never displayed during a match

### 🎮 UI Improvements
- **Mouse Hover Buttons Turn Theme Color**: Hovering over any interactive button renders it in the theme color (global effect, can be toggled in UI Improvements).
> The "right-to-left" slide-in animation of the main menu's right panel is built-in behavior and is **forced on**.

**Note: There is no longer a master "UI Improvements" toggle** — each sub-feature under this group is an **independent toggle**,
configured individually under "Other Features → UI Improvements" in the menu:

- **Main Menu Improvements**: Streamlines cluttered main menu elements — hides background noise, the left panel's backdrop and divider lines, window highlights, full-screen tint overlay, and friend request badges; **keeps the Among Us logo**
- **Main Menu Slide-In**: Clicking "Start / My Account / Credits" slides the right panel in from right to left; opening Settings, Announcements, Inventory, or Shop automatically slides it out to avoid blocking
- **Meeting Role Tags**: During meetings, each player's avatar has an `EditTag` icon; clicking it opens the game's original "Shapeshifter Menu" to choose a role and tag that player;
  Tags appear **next to the meeting name** and **above the player's head in-game**, Impostor red / Crewmate blue, Impostors first then Crewmates; a "Clear Tags" option is provided at the end of the list,
  and all tags are automatically cleared when the match ends (local tags, only visible to you; your own slot shows no icon)
- **Recap Info**: Press `F2` in the lobby to view last match's details (see "Recap Info" above)
- Force-show the Start button, skip kill animations
- Display improvements: better ping / cooldown display, sabotage cooldown display
- Room Info: Room search shows up to 10 rooms (scrollable), each row showing host name/platform/room code
- Color Name Display: Shows a player's color name near them
- Name Colors: Your and others' names display in their respective colors; from the Impostor's perspective, teammates show as red (applies to chat bubbles, meeting votes, and the meeting opening "who died / who reported" intro screen; the ejection result screen keeps the game's native look)
- Display-related: Show host in the top-left of meetings, show your own role, show Impostor teammates' roles, show everyone's roles after death
- Cosmetic Saving (6 preset buttons on the inventory page for one-click outfit changes)
- Unlock 240 FPS, unlock all cosmetics, skip disconnect penalty, auto return to lobby are separate toggles under "Display & Roles" (**also independent, not linked to other toggles**)
- Better Chat: Rich text input, copy-paste, bubble animations, dark theme
- Menu floating button

### 🚀 Startup Screen & Branding Replacement
- **Startup Update Check**: On game launch, first checks for mod updates on the splash screen, showing "Checking for available mod updates";
  Three possible results — network error (amber), current version outdated (red), current version up to date (green),
  with text transitions using **fade-out/fade-in**; **entry to the main menu is only allowed after the check completes** (with a 12-second hard timeout fallback so it never freezes).
- **Splash Screen**: Replaced with a mod-drawn wordmark + "Starting Game" and a spinner in the bottom-right, and the original startup sound is muted.
- **Loading Tip**: During match loading (same for host / member), shows "Setting up your game" + a spinner in the bottom-right.
- **Branding Replacement**: The original Among Us logo (main menu / loading screen / splash animation) is hidden and replaced with the mod wordmark.
### 🔍 Mod Client Detection

- Identifies and marks other compatible mod clients in the room

### 🎨 Personalization

- Multi-language: Chinese / English / Русский
- Adjustable UI theme and scale, can block telemetry/crash reporting

---

## Hotkeys

| Key | Function |
|:---:|:-----|
| `Insert` | Open / close the main menu |
| `F2` | Show / hide recap info (lobby only) |
| `F6` | Copy the current lobby code |

> Keys can be changed in the `BepInEx/config/` config file (`Keys.MenuKey` / `Keys.RecapKey` / `Keys.CopyCodeKey`).

---

## Installation

> Amethyst requires the **BepInEx (IL2CPP, Windows x64)** runtime.

1. Download and install **BepInEx BleedingEdge — IL2CPP (`win-x64`)**
2. Locate the Among Us installation directory:
   - **Steam**: Library → right-click Among Us → Manage → Browse Local Files
   - **Epic**: Library → Among Us → Manage, usually at `C:\Program Files\Epic Games\AmongUs`
3. Extract BepInEx into the game directory (same level as `Among Us.exe`)
4. Launch the game once to the main menu, then close it (this creates the `BepInEx/plugins` directory)
5. Place **`Amethyst_v1.0.8.dll`** into `BepInEx/plugins/`
6. Launch the game and press `Insert` to open the menu

> The first launch generates the config file in `BepInEx/config/`; delete the corresponding `.cfg` to reset settings.

---

## Anti-Cheat Notes

When anti-cheat triggers, a notification pops up and the configured action is taken (warn/kick/ban). Common detections include but are not limited to:

| Category | Description |
|:-----|:-----|
| Crewmate Kill | A Crewmate role performing a kill |
| Impostor Kills Impostor | An Impostor killing a teammate |
| Dead Player Kill | A dead player performing a kill |
| Repeatedly Killing Dead Players | Repeatedly killing already-dead targets |
| Crewmate Sabotage / Off-Map Sabotage | A Crewmate or an illegal position initiating a sabotage |
| Rapid Sabotage | Consecutive sabotages on different systems within a very short time |
| Venting / Sabotaging / Closing Doors During Meetings | Impossible actions during meetings |
| Unauthorized Venting / Forged Vent ID | Venting without permission or using a forged vent ID |
| Venting on Behalf of Others | Forcing others into/out of vents |
| Vent-Kick Exploit | vent-kick exploit usage |
| Forced Zipline | Forced zipline usage |
| Known Cheat Menu Signatures | Detected RPC signatures of known cheat menus |
| H&S Report/Sabotage/Door Close/Illegal Vent | Hide & Seek mode anomalies |
| Lobby Game RPC | Forged kill/meeting/shapeshift and other match RPCs before the game starts |
| RPC Flood | High-frequency RPC spamming from a single client |

---

## Building from Source

Requires Among Us game assembly references (`Assembly-CSharp.dll`, etc.). The project points to the game's unpacked reference directory via `GameRefsDir`.

```
dotnet build src/Amethyst.csproj -c Release
```

The build output is `src/bin/Release/netcoreapp6.0/Amethyst.dll` (plus a versioned copy `Amethyst_v1.0.8.dll`).

---

## Changelog
### v1.0.8
- Splash screen: Now checks for mod updates first (three results: network error / outdated / up to date, with fade transitions), and only allows entry to the main menu after the check completes;
  "Starting Game" + spinner is persistently shown in the bottom-right; custom wordmark and purple stick figure are not applicable.
- Splash screen text: During the loading phase, "Setting up your game" + spinner is shown in the bottom-right.
- UI Improvements: Added a "Mouse Hover Buttons Turn Theme Color" toggle (global effect, covering all interactive buttons).
- Loading screen: The original Among Us logo is replaced with the mod wordmark; the loading bar uses the original color scheme.
- Main menu improvements: Hidden the friends list background panel; removed theme switching, fixed to #A06EFF.
- Role Tags: Fixed "panel obscured by meeting nameplates after a death" (nameplates are temporarily hidden while the panel is open, restored on close).

### v1.0.7

- **Added Home Info**: A new independent card **"More Info"** on the home page (**System** (including architecture) / **Game Version** / **BepInEx Version** / **Number of Loaded Mods**); the "About" card keeps only the mod's own version and author
- **Independent Mod Notifications**: Anti-cheat / protection / management / mod detection notifications now use **the mod's own notification cards** (top of screen, no longer occupying the original bottom-left notification bar)
- **Added Startup Loading Screen**: Takes over the loading screen on game launch, with the mod wordmark (custom icon) + breathing glow + version number in the center, and "Starting Game" with a spinner ring in the bottom-right
- **Mod Notification Beautification**: Plain-text cards (**no player color rendering**), fade in/out fully synced with the card; player cosmetics are not shown
- **Removed "Show Platform and Level"**: The mod no longer sends join/leave notices; normal player join/leave is entirely handled by the original notifications
- **Frame Rate Unlock Now Adjustable**: The toggle is now "Unlock Frame Rate"; when enabled, you can slide (or tap arrows to fine-tune) to choose a frame rate cap between **60~240**, default 240; disabling returns to the original 60
- **Theme Fixed**: Removed the theme color selector; the UI theme is forced to **#A06EFF** (the Violet color code from the original list)
- **Added Main Menu Display Image**: Fill the main menu with your own prepared image (proportional cover, resolution-adaptive), and linked hiding of the original floating characters and white dots
- **Added Snow Effect**: Snow in the main menu, independent toggle; no scaling, doesn't block buttons, brightness increased
- **Added Skill Duration Decimal Display**: Besides cooldowns, skill **durations** are also shown with one decimal place
- **Added Cheat RPC Detection** (referencing FinalSpectrum): Identifies known cheat menu RPC signatures and merges them into anti-cheat (default: warn only)
- **Fixed Name Rendering**:
  - Names above heads being erased and colors being wrong when a Shapeshifter transforms
  - Names in the Shapeshifter menu not colored by player color
  - **The local player's own** name turning red after transforming (now the name color is forced to follow body color)
- **Fixed Mushroom Mixup Sabotage**: Names were not hidden and name colors not rendered during sabotage in Freeplay mode; now names are hidden during sabotage and automatically restored when it ends
- **Fixed Map Loading Progress for Room Members**: When a room member (not host), the progress bar was stuck at 30% "Generating Map" (the game only provides real loading progress on the host side); now it smoothly advances to about 68% and then waits for the map to be ready
- **Fixed Main Menu Slide-In Animation**: No animation when clicking "Credits" then "Start"; also fixed a frame drop caused by a per-frame `GameObject.Find`
- **Fixed Meeting Role Tag Panel**: Obscured by meeting nameplates, most cell characters disappearing, semi-transparent borders; panel size set to 1.1x
- **Fixed Ping / Frame Rate Display**: Moved to the topmost layer, position and letter spacing adjusted
- **Removed "Other Mod Detection"** feature (after testing, confirmed insufficient stability; removed entirely as requested)
- Version 1.0.6 → **1.0.8**

### v1.0.6

- **Added Meeting Role Tags**: In meetings, each player's avatar has an EditTag icon; clicking it opens the game's original "Shapeshifter Menu" to choose a role and tag that player;
  Tags appear next to the meeting name and above the player's head in-game, **Impostor red / Crewmate blue**, Impostors first then Crewmates; a "Clear Tags" option is provided at the end of the list, and all tags are automatically cleared when the match ends
- **Added Player History / Cheat History**: Forced on, file-only, never shown in any UI.
  Records each player's name / friend code / PUID / platform / level, and supports **reading back history** by friend code
- **Added Recap Info**: In the lobby, view last match's roles, kills / task progress, cause of death, and match result for each player
- **Fixed Blank "Settings" Page in Mod Menu**: Sub-tab array index out of bounds caused the entire page to render nothing
- **Fixed Several Recap Issues**: Disconnected players shown as "Alive", result faction possibly locked to the wrong faction, role lookup cache holding an empty table
- **Fixed Force-Quit Players' Names Showing as TESTNAME**
- **Fixed Main Menu Improvements**: Hidden effects being unexpectedly restored after opening settings
- Removed the experimental "Settings Page Color Scheme" feature (after testing, confirmed unsatisfactory)
- Version 1.0.5 → **1.0.6**

### v1.0.5

- **Added Main Menu Improvements** (merged into the "UI Improvements" master toggle): Streamlines cluttered main menu elements and keeps the Among Us logo
- **Added Main Menu Slide-In Animation**: Clicking "Start / My Account / Credits" slides the right panel in from right to left;
  opening Settings, Announcements, Inventory, or Shop automatically slides it out to avoid the panel blocking popups
- **Fixed Main Menu "Settings" Not Opening**: The original `OptionsMenuBehaviour.Open()` throws a null reference causing the settings popup not to display; fields are now filled in and fallbacks added
- **Fixed Meeting Opening Name Colors**: Fixed the reporter's and deceased's names not rendering in player colors on the "who died / who reported" intro screen;
  the ejection (exile result) screen is no longer additionally colored, keeping the game's native look
- **Cosmetic Saving / One-Click Switching**: 6 preset buttons provided on the inventory page
- Version 1.0.4 → **1.0.5**

---

## Disclaimer

Amethyst is an unofficial third-party modification mod for Among Us.

This project is not affiliated with, sponsored by, or endorsed by Innersloth LLC in any way. Among Us and its related trademarks and assets belong to their respective owners.

Amethyst is intended for use in private rooms only.

The software is provided "as is", without warranty of any kind. Installing or using it means you accept any consequences arising from it, including but not limited to account restrictions, bans, kicks, crashes, progress loss, file corruption, game instability, or incompatibility with future game updates. The developers are not responsible for any consequences, damages, or misuse arising from the use of this software.

---

## License

This project is released under **GNU GPL v3.0** (see [LICENSE](LICENSE)). You are free to use, study, share, and modify it, but derivative works must use the same license.

---

<div align="center">

Made with ❤️ by **一只屑小紫**

</div>
