# BetterCommunication

`BetterCommunication` is a BepInEx 5 plugin for `Super Battle Golf` focused on communication and quality-of-life UI fixes.

It adds:
- clickable Steam profile links on supported player names;
- clickable Steam profile links on supported player avatars;
- clickable URLs in in-game text chat;
- session chat history restore when the chat UI is recreated;
- optional chat history persistence across full game restarts.

This project is UI-only. It does not unlock content, alter progression, or modify gameplay state.

## Features

- `Match Setup`: click a player name or avatar to open that player's Steam profile.
- `Pause Menu`: click a player name or avatar to open that player's Steam profile.
- `Text Chat`: click a URL in chat to open it in your default browser.
- `Chat History`: by default, recent messages are kept in memory for the current launch and restored if the chat UI resets after a round.
- `Optional Persistence`: if you want chat history to survive a full restart, enable `PersistChatHistoryAcrossLaunches` in the config.

## Project Layout

- `src/BetterCommunication.csproj`
- `src/Plugin.cs`
- `src/ChatLinkTextHandler.cs`
- `src/ChatHistoryManager.cs`
- `src/SteamProfileClickTarget.cs`
- `src/SteamProfileLinkInjector.cs`

## Prerequisites

You need:
- `Super Battle Golf`
- `BepInExPack` for the game
- a .NET SDK installed locally

This workspace currently uses the system C# compiler through `build-local.bat`.

## Build Setup

Set an environment variable pointing to the game root:

```bat
set SUPER_BATTLE_GOLF_PATH=C:\Games\Super Battle Golf
```

Then build the project with your preferred IDE or CLI after installing a .NET SDK.

The project references the game's managed assemblies from:

```text
%SUPER_BATTLE_GOLF_PATH%\Super Battle Golf_Data\Managed
```

## Install

Copy the built DLL into:

```text
<Super Battle Golf>\BepInEx\plugins\marki-BetterCommunication\
```

## Config

The generated config file is:

```text
<profile>\BepInEx\config\local.marki.superbattlegolf.bettercommunication.cfg
```

Recommended defaults:

- `EnableChatLinks = true`
- `EnableChatHistory = true`
- `PersistChatHistoryAcrossLaunches = false`
- `EnableSteamProfileLinks = true`
- `EnableAvatarLinks = true`

## GitHub

Current repository name:

```text
betterCommunication
```

Suggested short repository description:

```text
UI-only communication improvements for Super Battle Golf: clickable Steam profiles, clickable chat URLs, and chat history restore.
```

Suggested topics:

```text
super-battle-golf bepinex unity modding thunderstore csharp
```

## Use

1. Start the game with BepInEx.
2. Open a supported player list UI such as match setup or the pause menu.
3. Click a player's nickname or avatar.
4. The player's Steam profile should open.
5. Paste a URL into text chat or click an existing URL in chat.
6. The link should open in the default browser.
7. Finish a round or otherwise force the chat UI to reset.
8. Recent chat messages from the current launch should be restored automatically.
