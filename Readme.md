# Railbound

Multiplayer Unity project by **oneEngine**. The game boots into a login menu (guest, Google, or account-based authentication against Nakama), then flows through a lobby into gameplay scenes.

## Requirements

| | |
|---|---|
| Unity | **2023.1.22f1** (see `ProjectSettings/ProjectVersion.txt`) |
| Render pipeline | Universal Render Pipeline 15.0.7 |
| Active build target | Standalone (Linux64) — Android supported (IL2CPP, min API 22 / target 29) |
| Git | Git LFS enabled (`.gitattributes`) |

Open the repository root as the Unity project — `Library/`, `Logs/`, `Temp/` and generated `*.csproj` / `*.sln` files are local artifacts and are not tracked.

## Scenes in build

| Build index | Scene | Path | Enabled |
|---|---|---|---|
| 0 | LoginMenu | `Assets/Project/LoginMenu.unity` | ✅ |
| 1 | GameLobby | `Assets/Project/GameLobby.unity` | ✅ |
| — | LoadingScreen | `Assets/Project/LoadingScreen.unity` | ❌ |

`Assets/Project/SceneIndexes.cs` is the code-side source of truth for scene flow. All other scenes in Build Settings (sandbox prototypes, asset-pack demos) are disabled.

## Project layout

```
Assets/
  Project/          # Main game: scenes (LoginMenu, GameLobby, LoadingScreen),
                    #   Scripts (LoginUI, LoginMenuController, AuthenticationService,
                    #   GameManager, BattleRoyaleLoadingScreen, SceneIndexes)
  Scenes/           # Gameplay + UI scenes (Game/Map1, UI/Menu Screens)
  SandBox/          # Prototypes: train, shooter AI, zombie, networking/Nakama
  TrueTrackSystem/  # Track system (+ demo scene)
  Settings/         # URP / render settings
  Resources/        # Runtime-loaded assets
```

## Key packages

- `com.unity.netcode.gameobjects` 1.7.1 + `com.unity.multiplayer.tools` — networking
- `com.heroiclabs.nakama-unity` v3.21.1 — matchmaking, sessions, auth backend
- `com.unity.inputsystem` 1.7.0, `com.unity.cinemachine` 2.9.7
- `com.unity.addressables` 2.9.1, `com.unity.textmeshpro` 3.0.6
- `com.kitwright.unity.mcp` — Unity MCP for AI-assisted editor work

## AI / MCP workflow

This project is set up for agent-assisted development:

- **`opencode.json` / `.mcp.json`** — KitWright Unity MCP server (HTTP, `127.0.0.1:9285`)
- **`CLAUDE.md`** — managed project skills (`unity-mcp-workflow`); edit outside its marker block only
- Inspect and modify scenes, prefabs, and ScriptableObjects **through the Unity MCP / Editor API**, never by patching `.unity`, `.prefab`, or `.asset` files directly
- After editing scripts externally, call `request_recompile` (rejected while in Play Mode — exit Play Mode first), then check compilation/console error resources

## Conventions

- Branch on structured tool results (`{success, code, error}`), not free-form text
- Resolve objects by `instanceId` after the first lookup instead of re-resolving by name
- Save only the scenes/prefabs intentionally modified, then read values back
