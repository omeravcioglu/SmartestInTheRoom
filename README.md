# Smartest in the Room

**An online party game for 1–8 players made in Unity 6.** A match is a run of fast elimination minigames until someone reaches 100 points. Everyone plays the same level at the same time. There are 69 games, from reflex tests to crazy golf, a sheepdog to steer and a maze carved fresh every level. A voiced host runs the show, and the whole game wears a hand-built "Tabloid" newspaper look.

---

## Gameplay

- **69 elimination minigames**, each with one line of rules: reaction, reflexes, memory, timing, aim, physics and nerve.
  - Everyone plays the *same* seeded level at the same time, and failing knocks you out.
  - Levels 1–2 are warm-ups. From level 3, if nobody fails, the slowest player goes. If everyone fails, the level comes back harder.
  - The last one standing wins. The payout is 1st +20, 2nd +10, 3rd +5 and last −5, all set in `GameConfig`.
  - A scoreline on the game panel shows how each level is going: pips (3 of 5 caught), a count (7 / 16) or a progress bar.
- **20 question rounds** also exist. They are game-theory dilemmas where your payoff depends on what the rest of the room picks.
  - Playtesters didn't enjoy them, so they are switched off (`GameConfig.questionRounds`).
  - Nothing was deleted, so they can be turned back on and dealt between minigames.
- Players are expected to talk over Discord, so there is no in-game chat.

**The 69 minigames:** Arrow Rush, Backwards, Bullseye, Buzz Wire, Catch, Chase, Chimp Test, Colour Match, Count the Dots, Darts, Decoy, Dodge, Echo, Fireflies, Fishing, Flap, Flash Point, Goalie, Good Catch, Green Light, Herd, Hoops, Hover, Juggle, Keep the Beat, Keepy Uppy, Lasers, Let Go, Maze, Memory Boxes, Mirror, Odd One Out, Pairs, Penalty, Pop, Pop the Lock, Pour, Press Your Luck, Putt, Quick Draw, Quick Math, Repaint, Rhythm, Ring, Road, Ruler Drop, Runaway, Shell Game, Simon, Slice, Sort It, Spot the Change, Spotlight, Stack, Statues, Steady Hand, Stop the Clock, Stroop, Tag, Tightrope, Timing Bar, Toss, Traffic, Trail, Twins, Type It, Whack, What's Missing?, Which Was First?

[`docs/MINIGAMES.md`](docs/MINIGAMES.md) lists every minigame with its tagline, rule and controls.

Signals never rely on red against green alone. Games say NOW / GO / STOP in words, number their cards, or use colors that also differ in brightness.

**Status:** playable prototype, with a first Windows release candidate (0.1.0, 29 Sep 2026).
- Playtests shaped it:
  - The first playtest found the questions too wordy and the minigames the most fun. That led to more minigames per match, a full UI rebuild in the Tabloid style, and a review pass that fixed more than 20 bugs.
  - Later playtesters found the questions not fun at all, so matches are now minigames only.
- 49 minigames have been added since the first playtest: mouse, catching, reflex, physics and memory games, each still one file.
- What exists:
  - the voiced host, with 150 of 165 lines recorded;
  - 159 EditMode tests and PlayMode smoke tests;
  - a one-click release builder that checks the project before it builds.
- Still open (see [`docs/SMARTEST_STATUS.md`](docs/SMARTEST_STATUS.md)):
  - A match across two machines hasn't been played yet. Only the solo host loop is tested.
  - Online play over Relay is untested.
  - Difficulty ramps are first drafts, except Tightrope's, which was tuned against a simulated player.
  - The intros for the 15 newest minigames aren't recorded yet, and there's no music yet.

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
| Testing | Unity Test Framework: 159 EditMode tests, plus PlayMode match-flow, minigame smoke and screenshot-tour tests |

## What I built

All game code lives in **`Assets/_Smartest/`**: 138 C# scripts, about 27k lines.

| System | Key scripts |
|---|---|
| Match flow: host-authoritative state machine (lobby → minigames → reveal → scoring → winner) | `Scripts/Rounds/GameState.cs` |
| Question rounds (switched off): scoring rules as data, round catalog and deck | `Scripts/Rounds/Resolvers.cs`, `RoundCatalog.cs`, `RoundDeck.cs` |
| Minigame framework: warm-up and elimination rules, payouts, shared seeded levels, one stage for every game, per-level scoreline, drawing kit | `Scripts/Minigames/Core/EliminationLadder.cs`, `PayoutTable.cs`, `LevelRng.cs`, `MinigameRegistry.cs`, `MinigameView.cs`, `MinigameStage.cs`, `Scoreline.cs`, `UiKit.cs`, `KitWatch.cs` |
| The 69 minigames (one class each) | `Scripts/Minigames/Games/*.cs` |
| Tabloid UI kit: color and type tokens, code-drawn ink art, stickers, stamps, headlines and effects | `Scripts/Core/Palette.cs`, `Typo.cs`, `FontSet.cs`, `Scripts/UI/Kit/InkSprites.cs`, `Ink.cs`, `HardShadow.cs`, `TextDrop.cs`, `Burst.cs` |
| HUD: masthead, race to 100, seat rail and seat cards, host caption | `Scripts/UI/Hud/*` |
| Networking and lobby | `Scripts/Net/NetSession.cs`, `PlayerData.cs`, `Scripts/UI/MenuUI.cs`, `LobbyUI.cs`, `LobbySeat.cs` |
| Game screens (question, reveal, winner, settings) | `Scripts/UI/GameUI.cs`, `RoundPanel.cs`, `RevealPanel.cs`, `WinnerPanel.cs`, `SettingsPanel.cs` |
| Voiced host and audio direction | `Scripts/Core/VoiceScript.cs`, `VoiceLines.cs`, `AudioDirector.cs`; code-generated SFX in `Sounds.cs` |
| Editor tooling | `Editor/SceneBuilder.cs`, `ReleaseBuilder.cs`, `VoiceStudioWindow.cs`, `VoiceWiring.cs`, `RetiredFileCleanup.cs` |
| Tests | EditMode: `ResolverTests`, `MinigameTests`, `DeckTests`, `TabloidTests`, `VoiceScriptTests`. PlayMode: `MatchFlowTests`, `MinigameSmokeTests`, `ScreenshotTour` |

The editor tooling covers:
- **`SceneBuilder`:** a one-click (and headless) builder that regenerates both scenes, the prefabs, the ink art, the font assets and all 89 challenge assets from code.
- **`ReleaseBuilder`:** a one-click (and headless) Windows release.
  - It refuses to build if the build scenes are wrong, a minigame has no challenge asset, or a font license file is missing.
  - It builds into a fresh versioned folder and moves the debug symbols out.
  - Every build ships with the SIL Open Font License texts.
- **Voice tools:** a voice-line recording studio that can also record missing lines headless from the command line, and the wiring of the recorded lines.
- **`RetiredFileCleanup`:** a cleanup tool for files retired in the redesign.

### Code highlights

- **`Scripts/Minigames/Core/EliminationLadder.cs`:** warm-up, elimination, tie-break and ranking rules in plain C# with no Unity dependency, fully unit-tested.
- **`Scripts/Minigames/Core/LevelRng.cs`:** every client builds the identical level from a shared seed.
  - Only the seed, the level number and one result per player per level travel over the network. The host never simulates a game.
  - Reaction times are measured on each player's own machine, so ping never costs a reaction.
  - The physics minigames step at a fixed 1/240 s, so frame rate doesn't change the ball.
- **`Scripts/Rounds/GameState.cs`:** the host-authoritative match state machine.
  - Clients send results with `[Rpc(SendTo.Server)]`, and the "GO" moment of each minigame is synced to server time.
  - With question rounds on, a locked-in answer is visible only to the host and the player who gave it until the reveal.
- **`Scripts/Minigames/Games/MazeGame.cs`:** a maze carved fresh for every level by a random depth-first search. The whole mouse stroke since the last frame is checked against the walls, so a quick flick can't jump through one.
- **`Scripts/Minigames/Games/TightropeGame.cs`:** an inverted pendulum stepped at 1/240 s. Its difficulty ramp was tuned against a simulated player with a quarter-second reaction time.
- **`Scripts/Net/NetSession.cs`:** session creation and joining over Relay, LAN and local modes, and clean handling of players who leave (the match ends if everyone else leaves).
- **`Tests/PlayMode/MinigameSmokeTests.cs`:** plays levels 1–12 of every minigame, as a player and as a spectator, and fails if any level throws or never ends.
- **`Tests/PlayMode/ScreenshotTour.cs`:** plays a solo match headless and saves a picture of every screen and minigame.

## Scenes

| Scene | Purpose |
|---|---|
| `Assets/_Smartest/Scenes/Menu.unity` | Front page: play, join, lobby, sound settings |
| `Assets/_Smartest/Scenes/Game.unity` | Match: minigames, reveal, winner |

Both scenes are generated by **Tools ▸ Smartest ▸ Build Scenes** (`Editor/SceneBuilder.cs`).

## Integrated third-party assets

This is almost entirely original code and code-drawn art. Imported content:
- **TextMesh Pro**.
- **Fonts:**
  - Archivo (Black, Condensed, Expanded and ExtraCondensed cuts);
  - Atkinson Hyperlegible Next and Mono.
  - All are from Google Fonts under the SIL Open Font License. Each license file sits next to its fonts in `Assets/_Smartest/Fonts`.
- **Leftovers from Unity's URP project template:** `TutorialInfo/`, `Scenes/SampleScene.unity` and `Readme.asset`. None of them is in the build.

## Design docs

- [`docs/MINIGAMES.md`](docs/MINIGAMES.md): every minigame with its tagline, rule and controls (generated from `MinigameRegistry.cs`)
- [`docs/SMARTEST_STATUS.md`](docs/SMARTEST_STATUS.md): current state, architecture, the playtest changes, release notes and open issues
- [`docs/SMARTEST_REDESIGN.md`](docs/SMARTEST_REDESIGN.md): design of the rounds and minigames
- [`docs/SMARTEST_VOICE_SCRIPT.md`](docs/SMARTEST_VOICE_SCRIPT.md): the host's voice script

`Tools/GenerateHostVoice.ps1` is a helper script for batch-generating host lines. **The ElevenLabs API key is never stored in the project:** it's read from Unity EditorPrefs or an environment variable.

## About this repository

This public repository is a **showcase**. It contains the documentation and the **148 source files I wrote** for this project. The complete project, including licensed third-party assets that cannot be redistributed, is kept in a private repository.

Copyright © Omer Avcioglu (McHunter Studio). **All rights reserved.** Viewing only; see [LICENSE](LICENSE).
