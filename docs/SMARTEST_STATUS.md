# Smartest in the Room — status

**Unity 6000.0.32f1 · URP · 2D/Canvas only · `E:\UnityProjects\SmartestInTheRoom`**
First to 100 points wins. 1–8 players online. No in-game chat (Discord). All code lives under
`Assets/_Smartest/`, namespace `Smartest`.

---

## What the game is now

A match alternates two kinds of challenge until somebody reaches 100:

**Social rounds (24).** Everyone answers the same question at the same time and the payoff
depends on what the rest of the room did. Red/Green, Yes/No, or a number from 1–10. One short
rule under the question, never on a button.

**Minigames (20).** Everyone plays the same level simultaneously. Fail and you're out of that
minigame; if nobody fails, the slowest goes; if everyone fails, the level comes back harder.
Last one standing wins. 1st +20, 2nd +10, 3rd +5, last −5 — all three numbers live on
GameConfig.

Flow per challenge:

```
social    RoundIntro -> Answering -> Reveal -> Scoring
minigame  RoundIntro -> (Play -> LevelResult)* -> Reveal -> Scoring
```

Nothing after Scoring changed: the 100-point check, the tie-break, the scoreboard and the
winner screen are the same as before.

## Architecture

| Area | Where | Notes |
|---|---|---|
| Match engine | `Scripts/Rounds/GameState.cs` | Host-authoritative NetworkBehaviour, spawned from `Resources/GameState.prefab` once every client has loaded the Game scene. Clients only react to networked field changes. |
| Social payoffs | `Scripts/Rounds/Resolvers.cs` + `RoundCatalog.cs` | One resolver per rule; rules and numbers are data. |
| Minigame rules | `Scripts/Minigames/Core/EliminationLadder.cs`, `PayoutTable.cs` | Pure C#, no Unity, fully unit-tested. |
| Minigames | `Scripts/Minigames/Games/*.cs` | One file each. Level content comes from `LevelRng.For(gameId, seed, level)`, so every client builds the identical level. |
| Minigame UI | `Scripts/Minigames/Core/MinigameStage.cs` + `UiKit.cs` | One panel hosts every game; games draw only through UiKit, which uses the same rounded sprite and the same Palette as the rest of the UI. |
| Networking | `Scripts/Net/NetSession.cs` | Netcode for GameObjects 2.13.2 + Multiplayer Services 2.3.1 (Relay sessions), plus LAN and 127.0.0.1 modes. |
| Everything generated | `Editor/SceneBuilder.cs` | **Tools ▸ Smartest ▸ Build Scenes** rebuilds folders, sprite, config, challenge assets, prefabs and both scenes. Manual scene edits are overwritten. |

**What goes over the wire during a minigame:** the seed, the level number, who is playing, and
one result per player per level (`SubmitLevelResultRpc`). The host never simulates a game.
Timings are measured on each player's own machine, so ping never costs a reaction; the phase
clock is Netcode server time, so "GO" lands on the same instant everywhere.

## Adding a minigame

1. `Scripts/Minigames/Games/MyGame.cs` — subclass `MinigameView`, fill in the level table,
   implement `Build()`, react in `OnTick()`, call `Finish(failed, metric)` once.
2. One line in `MinigameRegistry.All`.
3. **Tools ▸ Smartest ▸ Build Scenes**.

The deck, ladder, HUD, level-result beat, ranking screen, payout and tests pick it up
automatically. `GameState`, `GameUI`, `RoundDeck` and `SceneBuilder` are not touched.

## Tuning without code

`Assets/_Smartest/Data/GameConfig.asset`:
`minigamePlacePoints` (default 20/10/5), `minigameLastPlacePoints` (−5),
`minigameSoloClearPoints`, `minigameSoloLevels`, `minigameIntroSeconds`,
`minigameLeadInSeconds`, `levelResultSeconds`, `minigameMaxLevels`,
`alternateSocialAndMinigame`, plus the existing round timings and `targetScore`.

## Next time you open Unity

1. Let it compile.
2. **Tools ▸ Smartest ▸ Clean Up Retired Files** — deletes the trick-question and puzzle-grid
   files the redesign replaced. They still compile, they are simply unused.
3. **Tools ▸ Smartest ▸ Build Scenes** — regenerates the 44 challenge assets and both scenes.
4. **Window ▸ General ▸ Test Runner ▸ EditMode ▸ Run All** — expect everything green.
5. Play from `Menu.unity`. Two instances (or two PCs) to see a real elimination.

## Still unverified

- No end-to-end run has been confirmed yet. The Start-button / GameState-spawn fix from the
  earlier session has never been seen working.
- UGS Relay/Lobby enablement for this project. `GameConfig.networkMode = Lan` or `Local`
  avoids the cloud entirely if Relay misbehaves.
- Voice clips exist for the original 16 keys; the four new minigame keys
  (`minigame_start`, `level_up`, `you_are_out`, `minigame_winner`) have no audio yet, so they
  simply don't play.
- Minigame feel. Every level ramp is a first draft, meant to be tuned by playing.

## Known gaps and deliberate omissions

- **Press Your Luck has no live "X banked" feed.** Showing other players' banks mid-level
  would need per-frame networking, which nothing else in the game does. The fuse is shared,
  so the nerve contest is still real, but you can't watch someone else chicken out.
- **Sort It uses whole numbers only.** The design doc mentioned decimals; it ramps with more
  numbers and negatives instead, which is the same skill and easier to read.
- Grid puzzles are now drawn at runtime by each minigame rather than by the old shared
  `PuzzleGridView`, which is why that component is retired.

## Removed in the multiplayer-first redesign

Still Watching? (no decision), Honest Friends (everyone picks 1), Insurance (no other player
can affect it), Taxes (the answer changes nothing), and all four trick-question rounds plus
the 20-question bank (other players could only change the size of your prize, never whether
you won it). Remember the Squares, Symmetry and Simon came back as elimination minigames.

Strengthened because one option was always at least as good as the others: Rate This Game
(now needs an average of exactly 7), Take the Hit (a lone volunteer is now paid), Trolley
(nobody pulling now costs 10), 1-Up (a lone holdout is now paid), Charity (donors profit once
half the room joins in).
