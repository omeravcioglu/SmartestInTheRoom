# Smartest in the Room

**An online party game for 1–8 players made in Unity 6.** First to 100 points wins. Rounds alternate between game-theory dilemmas, where your payoff depends on what the rest of the room picks, and fast elimination minigames. A voiced host runs the show.

---

## Gameplay

A match alternates two kinds of challenge:

- **24 social rounds.** Everyone answers at the same time: Red/Green, Yes/No, or a number from 1 to 10. How many points you get depends on what everyone else chose. These are classic cooperate-or-betray dilemmas, so reading the room matters more than being right.
- **20 elimination minigames**, covering reaction, memory, timing, typing and perception. Everyone plays the *same* seeded level at the same time. Whoever fails, or is slowest, is knocked out, and the finishing order decides the payout.
- Players are expected to talk over Discord, so there is no in-game chat. The game focuses on the decisions.

**The 20 minigames:** Arrow Rush, Bullseye, Count the Dots, Dodge, Green Light, Keep the Beat, Memory Boxes, Mirror, Odd One Out, Press Your Luck, Quick Math, Simon, Sort It, Spot the Change, Steady Hand, Stop the Clock, Stroop, Timing Bar, Type It, Which Was First.

**Status:** playable prototype.
- All 44 challenges, the voiced host, about 120 unit tests and a Windows build exist.
- Still open (see [`docs/SMARTEST_STATUS.md`](docs/SMARTEST_STATUS.md)):
  - A full networked match hasn't been confirmed yet.
  - Online play over Relay is untested.
  - Difficulty ramps are first drafts.
  - There's no music or art pass yet.

## Tech stack

| Area | What it uses |
|---|---|
| Engine | **Unity 6** (6000.0.32f1), **URP** 17 |
| Networking | **Netcode for GameObjects** 2.13 + Unity Transport. Host / listen-server model; the host owns all game state. |
| Online services | **Unity Multiplayer Services** 2.3 *Sessions* over **Relay** (private sessions, join code, session locks when the game starts); **Unity Authentication** (anonymous sign-in) |
| Offline / LAN | Falls back to local-network hosting when services are unreachable (host IP as join code, port 7777). `LOCAL` connects two copies on one PC. |
| Input | Unity **Input System** |
| Audio | 132 voice lines generated with **ElevenLabs** TTS; sound effects synthesized in code |
| Testing | Unity Test Framework (EditMode), ~120 test cases |

## What I built

All game code lives in **`Assets/_Smartest/`**: 68 C# scripts, about 12k lines.

| System | Key scripts |
|---|---|
| Match flow: host-controlled state machine (lobby → rounds → reveal → scoreboard → winner) | `Scripts/Rounds/GameState.cs` |
| Social rounds: scoring rules as data, round catalog and deck | `Scripts/Rounds/Resolvers.cs`, `RoundCatalog.cs`, `RoundDeck.cs` |
| Minigame framework: elimination and tie-break ranking, payouts, shared seeded RNG, views/stages, UI kit | `Scripts/Minigames/Core/EliminationLadder.cs`, `PayoutTable.cs`, `LevelRng.cs`, `MinigameRegistry.cs`, `MinigameView.cs`, `MinigameStage.cs`, `UiKit.cs` |
| The 20 minigames (one class each) | `Scripts/Minigames/Games/*.cs` |
| Networking and lobby | `Scripts/Net/NetSession.cs`, `PlayerData.cs`, `MenuUI.cs`, `LobbyUI.cs` |
| Game UI (answer reveal, scoreboard, panels, tweening) | `GameUI.cs`, `RevealPanel.cs`, `ScoreboardUI.cs`, `Panel.cs`, `Tween.cs` |
| Voiced host and audio direction | `VoiceScript.cs`, `VoiceLines.cs`, `AudioDirector.cs`; code-generated SFX in `Scripts/Core/Sounds.cs` |
| Editor tooling: one-click builder for both scenes, prefabs and all 44 challenge assets; voice-line recording studio | `Editor/SceneBuilder.cs`, `Editor/VoiceStudioWindow.cs` |
| Tests | `Tests/EditMode/ResolverTests.cs`, `MinigameTests`, `DeckTests`, `VoiceScriptTests` |

### Code highlights

- **`Scripts/Minigames/Core/EliminationLadder.cs`:** elimination, tie-break and ranking rules in plain C# with no Unity dependency, fully unit-tested.
- **`Scripts/Minigames/Core/LevelRng.cs`:** every client builds the identical level from a shared seed, so only the seed, the level number and the results travel over the network.
- **`Scripts/Net/NetSession.cs`:** session creation and joining over Relay, with a local-network fallback and clean disconnect handling.
- **`Scripts/Rounds/GameState.cs`:** the host-controlled match state machine. Clients send answers with `[Rpc(SendTo.Server)]`, and the "GO" moment of each minigame is synced to server time.
- **`Scripts/Rounds/Resolvers.cs`** + **`Tests/EditMode/ResolverTests.cs`:** round scoring checked against an independent reference model.

## Scenes

| Scene | Purpose |
|---|---|
| `Assets/_Smartest/Scenes/Menu.unity` | Main menu, host/join, lobby |
| `Assets/_Smartest/Scenes/Game.unity` | Match: rounds, minigames, scoreboard |

`Editor/SceneBuilder.cs` can regenerate both scenes, the prefabs and the challenge assets.

## Integrated third-party assets

This is almost entirely original code. Imported content is limited to **TextMesh Pro** and leftovers from Unity's URP project template (`TutorialInfo/`, `Scenes/SampleScene.unity`).

## Design docs

- [`docs/SMARTEST_STATUS.md`](docs/SMARTEST_STATUS.md): status and open issues
- [`docs/SMARTEST_REDESIGN.md`](docs/SMARTEST_REDESIGN.md): design of the rounds and minigames
- [`docs/SMARTEST_VOICE_SCRIPT.md`](docs/SMARTEST_VOICE_SCRIPT.md): the host's voice script

`Tools/GenerateHostVoice.ps1` is a helper script for batch-generating host lines. **The ElevenLabs API key is never stored in the project:** it's read from Unity EditorPrefs or an environment variable.

## About this repository

This public repository is a **showcase**. It contains the documentation and the **72 source files I wrote** for this project. The complete project, including licensed third-party assets that cannot be redistributed, is kept in a private repository.

Copyright © Omer Avcioglu (McHunter Studio). **All rights reserved.** Viewing only; see LICENSE.
