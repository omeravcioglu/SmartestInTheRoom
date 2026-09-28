# Smartest in the Room

**An online party game for 1–8 players made in Unity 6.** First to 100 points wins. A match deals a game-theory question, where your payoff depends on what the rest of the room picks, then two fast elimination minigames, then another question, and so on. A voiced host runs the show, and the whole game wears a hand-built "Tabloid" newspaper look.

---

## Gameplay

A match alternates two kinds of challenge (by default one question, then two minigames):

- **20 social rounds.** Everyone answers at the same time: Red/Green, Yes/No, or a number from 1 to 10. How many points you get depends on what everyone else chose. These are cooperate-or-betray dilemmas, so reading the room matters more than being right.
  - Every rule is meant to be a real decision: other players always change your result, and no answer is always best.
  - Rounds that need three or more players are never dealt in a two-player match.
- **30 elimination minigames**, mostly quick mouse games with one line of rules: reaction, memory, timing, aim and nerve.
  - Everyone plays the *same* seeded level at the same time, and failing knocks you out.
  - Levels 1–2 are warm-ups. From level 3, if nobody fails, the slowest player goes. If everyone fails, the level comes back harder.
  - The last one standing wins. The payout is 1st +20, 2nd +10, 3rd +5 and last −5, all set in `GameConfig`.
- Players are expected to talk over Discord, so there is no in-game chat. The game focuses on the decisions.

**The 30 minigames:** Arrow Rush, Bullseye, Buzz Wire, Chase, Count the Dots, Dodge, Flap, Flash Point, Green Light, Hover, Keep the Beat, Keepy Uppy, Lasers, Memory Boxes, Mirror, Odd One Out, Pop, Press Your Luck, Quick Math, Shell Game, Simon, Sort It, Spot the Change, Stack, Steady Hand, Stop the Clock, Stroop, Timing Bar, Type It, Which Was First.

**Status:** playable prototype, reworked after the first playtest in September 2026.
- The first playtest found the questions too wordy and the minigames the most fun, which led to:
  - two minigames per question;
  - ten new mouse minigames;
  - a full UI rebuild in the Tabloid style;
  - a review pass that fixed more than 20 bugs and rewrote the weak rounds.
- The voiced host (116 of 126 lines recorded), 158 EditMode tests and PlayMode smoke tests exist.
- Still open (see [`docs/SMARTEST_STATUS.md`](docs/SMARTEST_STATUS.md)):
  - Only the solo host loop has been run end to end. A match across two machines hasn't been tested yet.
  - Online play over Relay is untested.
  - Difficulty ramps are first drafts.
  - The ten newest minigames have no voiced intro yet, and there's no music yet.

## Tabloid UI

The whole interface is drawn in a newspaper style:
- **Look:** newsprint paper with one ink and one gold, headlines with a gold drop, stickers and stamps.
- **Color rules:**
  - Red and green only ever mean an answer. Red also carries a diamond and green a circle, so color is never the only cue.
  - Gold means points gained and blue means points lost.
- **In-game screen:** a masthead with *the race to 100*, the stage, a caption bubble for the host (captions can be switched off), and a seat rail showing who is playing, done or out.
- **Winner screen:** a full front page.
- **Code-drawn art:** every shape is drawn in code and baked into sprites by the editor builder. There is no imported art.
- **Layouts:** built on a 1920 × 1080 frame inside an Expand-mode canvas, so 16:10 and ultrawide screens get more paper instead of a cropped page.
- **Fonts:** Archivo and Atkinson Hyperlegible.

## Tech stack

| Area | What it uses |
|---|---|
| Engine | **Unity 6** (6000.0.32f1), **URP** 17, 2D / Canvas UI |
| Networking | **Netcode for GameObjects** 2.13 + Unity Transport. Host-authoritative: the host owns all game state. |
| Online services | **Unity Multiplayer Services** 2.3 *Sessions* over **Relay** (private sessions, join code, session locks when the game starts); **Unity Authentication** (anonymous sign-in) |
| Offline / LAN | LAN hosting (host IP as join code, port 7777) and a `LOCAL` mode that connects two copies on one PC, so the game runs even without cloud services |
| Input | Unity **Input System** |
| UI | uGUI + **TextMesh Pro**, dynamic font assets generated from TTFs (Turkish glyphs included) |
| Audio | Host voice lines generated with **ElevenLabs** TTS; sound effects synthesized in code |
| Testing | Unity Test Framework: 158 EditMode tests, plus PlayMode match-flow, minigame smoke and screenshot-tour tests |

## What I built

All game code lives in **`Assets/_Smartest/`**: 98 C# scripts, about 20k lines.

| System | Key scripts |
|---|---|
| Match flow: host-authoritative state machine (lobby → question / minigames → reveal → scoring → winner) | `Scripts/Rounds/GameState.cs` |
| Social rounds: scoring rules as data, round catalog and deck | `Scripts/Rounds/Resolvers.cs`, `RoundCatalog.cs`, `RoundDeck.cs` |
| Minigame framework: warm-up and elimination rules, payouts, shared seeded levels, one stage for every game, drawing kit | `Scripts/Minigames/Core/EliminationLadder.cs`, `PayoutTable.cs`, `LevelRng.cs`, `MinigameRegistry.cs`, `MinigameView.cs`, `MinigameStage.cs`, `UiKit.cs`, `KitWatch.cs` |
| The 30 minigames (one class each) | `Scripts/Minigames/Games/*.cs` |
| Tabloid UI kit: color and type tokens, code-drawn ink art, stickers, stamps, headlines and effects | `Scripts/Core/Palette.cs`, `Typo.cs`, `FontSet.cs`, `Scripts/UI/Kit/InkSprites.cs`, `Ink.cs`, `HardShadow.cs`, `TextDrop.cs`, `Burst.cs` |
| HUD: masthead, race to 100, seat rail and seat cards, host caption | `Scripts/UI/Hud/*` |
| Networking and lobby | `Scripts/Net/NetSession.cs`, `PlayerData.cs`, `Scripts/UI/MenuUI.cs`, `LobbyUI.cs`, `LobbySeat.cs` |
| Game screens (question, reveal, winner, settings) | `Scripts/UI/GameUI.cs`, `RoundPanel.cs`, `RevealPanel.cs`, `WinnerPanel.cs`, `SettingsPanel.cs` |
| Voiced host and audio direction | `Scripts/Core/VoiceScript.cs`, `VoiceLines.cs`, `AudioDirector.cs`; code-generated SFX in `Sounds.cs` |
| Editor tooling | `Editor/SceneBuilder.cs`, `VoiceStudioWindow.cs`, `VoiceWiring.cs`, `RetiredFileCleanup.cs` |
| Tests | EditMode: `ResolverTests`, `MinigameTests`, `DeckTests`, `TabloidTests`, `VoiceScriptTests`. PlayMode: `MatchFlowTests`, `MinigameSmokeTests`, `ScreenshotTour` |

The editor tooling covers:
- **`SceneBuilder`:** a one-click (and headless) builder that regenerates both scenes, the prefabs, the ink art, the font assets and all 50 challenge assets from code.
- **Voice tools:** a voice-line recording studio and the wiring of the recorded lines.
- **`RetiredFileCleanup`:** a cleanup tool for files retired in the redesign.

### Code highlights

- **`Scripts/Minigames/Core/EliminationLadder.cs`:** warm-up, elimination, tie-break and ranking rules in plain C# with no Unity dependency, fully unit-tested.
- **`Scripts/Minigames/Core/LevelRng.cs`:** every client builds the identical level from a shared seed.
  - Only the seed, the level number and one result per player per level travel over the network. The host never simulates a game.
  - Reaction times are measured on each player's own machine, so ping never costs a reaction.
  - The physics minigames step at a fixed 1/240 s, so frame rate doesn't change the ball.
- **`Scripts/Rounds/GameState.cs`:** the host-authoritative match state machine.
  - Clients send answers with `[Rpc(SendTo.Server)]`, and the "GO" moment of each minigame is synced to server time.
  - A locked-in answer is visible only to the host and the player who gave it until the reveal, so nobody can peek through network traffic.
- **`Scripts/Net/NetSession.cs`:** session creation and joining over Relay, LAN and local modes, and clean handling of players who leave (the match ends if everyone else leaves).
- **`Tests/PlayMode/MinigameSmokeTests.cs`:** plays levels 1–12 of every minigame, as a player and as a spectator, and fails if any level throws or never ends.
- **`Tests/PlayMode/ScreenshotTour.cs`:** plays a solo match headless and saves a picture of every screen and minigame.

## Scenes

| Scene | Purpose |
|---|---|
| `Assets/_Smartest/Scenes/Menu.unity` | Front page: play, join, lobby, sound settings |
| `Assets/_Smartest/Scenes/Game.unity` | Match: questions, minigames, reveal, winner |

Both scenes are generated by **Tools ▸ Smartest ▸ Build Scenes** (`Editor/SceneBuilder.cs`).

## Integrated third-party assets

This is almost entirely original code and code-drawn art. Imported content:
- **TextMesh Pro**.
- **Fonts:**
  - Archivo (Black, Condensed, Expanded and ExtraCondensed cuts);
  - Atkinson Hyperlegible Next and Mono.
  - All are from Google Fonts under the SIL Open Font License. Each license file sits next to its fonts in `Assets/_Smartest/Fonts`.
- **Leftovers from Unity's URP project template:** `TutorialInfo/`, `Scenes/SampleScene.unity` and `Readme.asset`.

## Design docs

- [`docs/SMARTEST_STATUS.md`](docs/SMARTEST_STATUS.md): current state, architecture, the playtest changes and open issues
- [`docs/SMARTEST_REDESIGN.md`](docs/SMARTEST_REDESIGN.md): design of the rounds and minigames
- [`docs/SMARTEST_VOICE_SCRIPT.md`](docs/SMARTEST_VOICE_SCRIPT.md): the host's voice script

`Tools/GenerateHostVoice.ps1` is a helper script for batch-generating host lines. **The ElevenLabs API key is never stored in the project:** it's read from Unity EditorPrefs or an environment variable.

## About this repository

This public repository is a **showcase**. It contains the documentation and the **103 source files I wrote** for this project. The complete project, including licensed third-party assets that cannot be redistributed, is kept in a private repository.

Copyright © Omer Avcioglu (McHunter Studio). **All rights reserved.** Viewing only; see [LICENSE](LICENSE).
