# Smartest in the Room — Multiplayer-First Redesign

**Status: PROPOSAL. Nothing in this document is implemented yet.** Read it, change what you want, say "go", and I start.

**Art direction: unchanged.** Same palette (dark #16171D background, #21232E panels, gold #F5C542 accent, red #E5484D, green #30A46C), same rounded 9-slice panels, same `AnswerButton`, same fonts, same fade/scale/slide transitions. Every new screen in this proposal is assembled from the primitives that already exist (`Panel`, `AnswerButton`, `GridCell`, `PuzzleGridView`, `CountdownBar`, `Palette`, `Tween`). Nothing gets a new look.

---

## 1. Review of the current project

### 1.1 The test I applied to every round

A round stays only if **both** hold:

1. What the *other* players do changes *your* result.
2. You have a real decision — no option that is always at least as good as the others, whatever the room does.

### 1.2 Remove

| Round | Why it fails the test |
|---|---|
| **C06 Still Watching?** | NO pays 5 no matter what; YES pays 2 only if every single player says YES. Nobody ever has a reason to press YES. No decision. |
| **C23 Honest Friends** | Lowest number wins and ties share the prize, so everyone picks 1. It is also a weaker copy of The Godfather and Lowest Unique. |
| **C24 Insurance** | Your outcome depends only on your own choice; no other player can touch it. It is also the only reason the `Insured` flag and `GrantInsurance` exist — removing it simplifies scoring. |
| **C25 Taxes** | The answer changes nothing. A joke, not a decision. |
| **29–32 Trick questions** (+ the 20-question bank, `TrickQuestions.cs`, `TrickSplitResolver`, `InputType.Choice`) | A quiz. Other players only change the *size* of the prize; they cannot affect whether you get it. Single-player by your definition. |
| **26 Remember the Squares, 27 Symmetry, 28 Simon** | *Converted, not deleted.* In their current shape they are solo puzzles with a shared pot. They come back as elimination minigames (Memory Boxes, Mirror, Simon) where you race the room, not the puzzle. The grid code (`GridCell`, `PuzzleGridView`, `PuzzleGen`) is reused as-is. |

Net result: 32 rounds today → **21 kept social rounds (5 of them strengthened) + 3 new social rounds + 20 minigames = 44 challenges.**

### 1.3 Strengthen (kept, but the decision was too weak)

| Round | Problem | Change |
|---|---|---|
| **C12 Rate This Game** | Average ≥ 7 → everyone +3. Everyone rates 10, always. Zero tension. | Average must land on **exactly 7** → everyone +10, otherwise 0. Now you have to read what the others will type. |
| **C13 Take the Hit** | Staying quiet is never worse than volunteering (−5 either way in the worst case). | A **lone** volunteer gains 10 and everyone else gains 5. Several volunteers: each −5. Nobody: everyone −5. Volunteering now has a prize *and* a risk. |
| **C16 Trolley** | Pulling costs 5 for certain; not pulling costs 3 at worst. Never pull. | Nobody-pulls penalty becomes **−10**. Somebody had better pull — a proper volunteer's dilemma. |
| **C17 1-Up** | Reaching is never worse than holding back. | Holding back pays **+10 if you are the only one** who held back. |
| **C19 Charity** | Donating only costs you. | If **half or more** donate, every donor also gets +5. It becomes a coordination play, not just kindness. |

### 1.4 Rules: one short sentence, never inside a button

Every rule is rewritten to the shape of your example: at most two sentences, roughly 25 words, the numbers in bold, shown in the rules block under the question. Buttons show only the choice word (RED / GREEN / YES / NO / PULL / WAIT). The exact on-screen text for every round is in section 2. The long paragraphs I wrote last time are gone.

### 1.5 Reuse — what already exists and carries the new design

| Existing system | Reused for |
|---|---|
| `GameState` phase machine and its NetworkVariables (`PhaseEndTime`, `PhaseDuration`, `SubRound`, `Results`, `RevealLine`, `TieBreak`, `RoundIndex`) | Minigames run on the same machine. `SubRound` becomes the level number; the countdown is the same countdown. |
| `PlayerData.CurrentAnswer` + `SubmitAnswerRpc(int)` + `LockedIn` | A minigame result is still one int per player (the metric) plus a fail flag. `LockedIn` becomes the "finished this level" indicator. |
| `RoundDeck` | Becomes two decks (social, minigame) drawn alternately. Same no-repeat logic. |
| `RevealPanel` / `RevealRowView` | The ranking screen: same rows, the chip says 1ST / 2ND / … / OUT instead of RED / GREEN. |
| `ScoreboardUI`, `WinnerPanel`, `LobbyUI`, `MenuUI`, `NetSession`, tie-break at 100, `GameConfig`, `VoiceLines` | Untouched (a handful of new voice keys). |
| `Panel`, `Tween`, `Palette`, `AnswerButton`, `CountdownBar`, `GridCell`, `PuzzleGridView`, `KeyInput` | Every minigame view is built from these — that is what keeps the art identical. |
| `PuzzleGen` (Memory / Symmetry / Sequence) | Level generators for Memory Boxes, Mirror and Simon, with the level number driving size and count. |
| `oracle.py` + generated `ResolverTests.cs` | Regenerated for the changed payoffs; new pure-C# tests for the ladder and payouts. |

### 1.6 How minigames connect to the existing round and scoring architecture

A social round is unchanged: **RoundIntro → Answering → Reveal → Scoring**.

A minigame is the same shape with a loop in the middle:

```
RoundIntro (rule card, 2.5 s, same panel)
   → Play level 1 (5–20 s, everyone simultaneously)
   → LevelResult (1.5 s: "OUT: Ayşe, Mert")
   → Play level 2 → LevelResult → … until one player is left
   → Reveal (final ranking, same reveal rows)
   → Scoring (payout table applies the deltas, same count-up)
```

Nothing after Scoring changes: the 100-point check, the tie-break, the next round, the winner screen.

**What goes over the network.** The host sends three numbers: `Seed`, `Level`, `AliveMask`. Every client generates *identical* level content from `(gameId, seed, level)` with a deterministic RNG — the same falling blocks, the same lit boxes, the same bust time. Each client measures its own result locally and reports `(failed, metric)` with one RPC. The host never simulates a game; it only runs the ladder and the payout. This is exactly how answers work today, so no new replication pattern is introduced.

**Fairness.** "GO" is scheduled on Netcode server time, so the level starts at the same wall-clock instant on every machine regardless of ping. Reaction times are measured on the player's own machine, so a 120 ms ping does not cost a 120 ms reaction. Results are trusted from the client, the same as answers are today — it's a game between friends, not a tournament.

---

## 2. Social rounds — final list with the exact on-screen rule

Button words are what the two buttons say. "1–10" means the number grid. Bold numbers are bold on screen.

| Id | Title | Buttons | Rule shown on screen | Status |
|---|---|---|---|---|
| C01 | The Button | RED / GREEN | If EVERYONE picks Green, everyone gets **+10**. If anyone picks Red, Reds get **+15** and Greens lose **5**. All Red: everyone loses **10**. | kept — your numbers (see open question 1) |
| C02 | Pick a Pill | RED / GREEN | Red: **+8**, but only if fewer than half of you pick Red. Green: **+3**, guaranteed. | kept (prompt becomes "Red pill or green pill?") |
| C03 | The Snap | RED / GREEN | If EXACTLY half of you pick Red: Reds **+10**, Greens **−10**. Any other split: nothing happens. | kept |
| C04 | The Door | RED / GREEN | Green climbs on the door. Alone: **+15**. With anyone else: each **−5**. Red stays in the water: **0**. | kept |
| C05 | Rule One | 1–10 | If everyone picks a different number, everyone gets **+5**. If any two match, nobody gets anything. | kept |
| C07 | Lowest Unique | 1–10 | The lowest number that exactly ONE player picked wins **+15**. | kept |
| C08 | Two Thirds | 1–10 | Closest to two-thirds of the room's average wins **+10**. Ties split it. | kept |
| C09 | The Pot | 0–10 | Everything put in is doubled and split equally between ALL players — even those who gave nothing. | kept |
| C10 | Silent Auction | 1–10 | Highest bid wins **+20** (ties split it). Everyone pays their own bid, win or lose. | kept |
| C11 | Greedy | 1–10 | If everyone's numbers add up to **6 per player** or less, you each get what you asked for. One over: everyone gets **0**. | kept |
| C12 | Rate This Game | 1–10 | If the average of all ratings is EXACTLY **7**, everyone gets **+10**. Otherwise nothing. | strengthened |
| C13 | Take the Hit | VOLUNTEER / STAY QUIET | Exactly one volunteer: they get **+10**, everyone else **+5**. Several volunteers: each **−5**. Nobody: everyone **−5**. | strengthened |
| C14 | Attack the Leader | RED / GREEN | Red attacks: the leader loses **15**, each attacker pays **3**. Green: nothing happens. | kept |
| C15 | The Lever | PULL / WAIT | Pull to split the pot with everyone else who pulls now. Wait and the pot grows **+5**. Nobody pulls in 5 rounds: everyone **+20**. | kept (5 sub-rounds) |
| C16 | Trolley | PULL / DON'T | Pull costs you **5**. If nobody pulls, everyone loses **10**. | strengthened |
| C17 | 1-Up | RED / GREEN | Green reaches: **+5** — but if EVERYONE reaches, nobody gets it. Red holds back: **+10** if you're the only Red. | strengthened |
| C18 | Is This a Dream? | YES / NO | The bigger side gets **+3**, the smaller side **−3**. Exact split: everyone **−1**. | kept |
| C19 | Charity | DONATE / KEEP | Donate: pay **3** to last place. If half or more of you donate, every donor also gets **+5**. | strengthened |
| C20 | Predict the Room | 0–10 | Guess how many players will press RED next round. Exactly right: **+5**. Off by one: **+2**. | kept |
| C21 | The Godfather | 1–10 | Lowest number wins **+10** — if only ONE player picked it. Shared lowest: those players lose **10**. | kept |
| C22 | Sus | RED / GREEN | The colour FEWER players pick wins **+10** each. Even split: nothing. | kept |
| C26 | Pairs | 1–10 | **+10** if EXACTLY one other player picked your number. Otherwise **0**. | new |
| C27 | Sacrifice | GIVE / KEEP | Give costs you **5**. If at least half of you give, EVERYONE gets **+15**. | new |
| C28 | Bandwagon | 1–10 | Everyone on the MOST popular number gets **+5**. If all of you pick the same number, **+10** each. | new |

Ids 26–28 are freed by the removed mind games and reused. Removed: C06, C23, C24, C25, 29–32.

---

## 3. The minigame framework — rules shared by every minigame

These are the rules the ladder enforces for all 20 games, so every game explains only its own mechanic.

**Play.** Everyone still in plays every level at the same moment. A level lasts 5–20 seconds. Level 1 is easy; every following level is harder in a way that is specific to the game (section 4 lists the exact ramp).

**Out.** Fail the level and you are out of *this minigame only*; you watch the rest and keep your score. If nobody fails, the **slowest / least accurate** player is out, so every level removes at least one player and the minigame always ends. If *everyone* fails, nobody is out and the same level is replayed one notch harder for the survivors ("Again. Harder.").

**Winner.** The last player standing is 1st. Everyone else is ranked by how late they went out; players who went out on the same level are ordered by that level's metric (time or accuracy). Two players with an *identical* metric on the same level play a tie-break level between only themselves — never a coin flip (see open question 2).

**Safety cap.** After 10 levels the survivors are ranked by cumulative metric and the minigame ends. With "nobody fails → worst is out" this cap is practically never reached: 8 players need at most 7 levels.

**Solo (1 player).** No ranking. Three levels; clear all three for +20, otherwise 0.

**Scoring — the payout table.** Three numbers in `GameConfig`, editable in the Inspector:

```
minigamePlacePoints     = { 20, 10, 5 }   // 1st, 2nd, 3rd
minigameLastPlacePoints = -5              // last place, when 2+ players
minigameSoloClearPoints = 20
```

Rule: place *n* gets `placePoints[n-1]` if that entry exists; last place gets `lastPlacePoints` instead (last place overrides the table); everyone in between gets 0. That produces:

| Players | 1st | 2nd | 3rd | 4th | 5th | 6th | 7th | 8th |
|---|---|---|---|---|---|---|---|---|
| 2 | +20 | −5 | | | | | | |
| 3 | +20 | +10 | −5 | | | | | |
| 4 | +20 | +10 | +5 | −5 | | | | |
| 5 | +20 | +10 | +5 | 0 | −5 | | | |
| 8 | +20 | +10 | +5 | 0 | 0 | 0 | 0 | −5 |

Changing the table to `{ 20, 12, 8, 4 }` or adding a 4th paid place is a one-line edit; nothing else changes.

**Timing.** The host opens a level with a start time 1 s in the future (server clock); every client shows a 3-2-1 and starts at that instant. The host waits until every alive player has reported or the level deadline plus 1 s grace has passed; a missing report counts as a fail.

---

## 4. The 20 minigames

All 20 are simultaneous, 5–20 s per level, one input each, and each is a different mechanic (reaction, visual timing, internal clock, rhythm, risk, set memory, sequence memory, change detection, spatial reasoning, visual search, precision, steadiness, movement, key sequence, inhibition, order perception, arithmetic, estimation, ordering, typing). "Out when" is the fail condition; "Rank by" is the metric that orders players who survived or fell on the same level (and that decides who goes out when nobody fails).

### 4.1 Keyboard — Space

| # | Game | Rule shown on screen | Difficulty ramp (L1 → L5, then keeps climbing) | Out when | Rank by |
|---|---|---|---|---|---|
| 1 | **Green Light** | Wait for GREEN. Press Space the moment it appears. Press early and you're out. | L1 green after 1–3 s · L2 1–5 s · L3 red and gold decoy flashes · L4 more decoys, 0.5–6 s · L5 must press within 0.6 s of green | pressed before green, or later than the allowed window | reaction time |
| 2 | **Timing Bar** | A marker slides along the bar. Press Space while it's inside the gold zone. | L1 slow, zone 30 % · L2 zone 20 % · L3 faster, 14 % · L4 the zone drifts · L5 speed varies, zone 8 % | press outside the zone, or no press in 8 s | distance from zone centre |
| 3 | **Stop the Clock** | Stop the clock as close to **5.00 s** as you can. The numbers hide after 2 seconds. | L1 visible 2 s, ±0.50 s · L2 visible 1 s, ±0.35 · L3 hidden from 0, ±0.25 · L4 target 7.00 s, ±0.20 · L5 target 3.50 s, ±0.12 | error larger than the tolerance | absolute error |
| 4 | **Keep the Beat** | Four beats pulse. Then press Space on beats 5, 6, 7 and 8 — with no pulses to help you. | L1 100 BPM, ±150 ms · L2 120 BPM, ±120 · L3 90 BPM, 5 silent beats · L4 140 BPM, ±90 · L5 150 BPM, 6 silent beats, ±70 | any press outside tolerance, or a missed beat | total timing error |
| 5 | **Press Your Luck** | The bank rises. Press Space to keep it before it busts. Bust = 0. Lowest bank is out. | Bust hidden between: L1 6–12 s · L2 4–10 s · L3 3–8 s · L4 2–7 s · L5 1.5–6 s. The bust time is the *same* for everyone — you're all holding your nerve against one fuse. | busted, or never pressed | highest bank |

### 4.2 Keyboard — number keys / WASD / typing

| # | Game | Rule shown on screen | Difficulty ramp | Out when | Rank by |
|---|---|---|---|---|---|
| 6 | **Stroop** | Press **1** for RED, **2** for GREEN — the INK colour, not the word. 5 in a row. | L1 5 prompts, word matches ink · L2 half mismatch · L3 6 prompts, 1.2 s each · L4 8 prompts, 0.9 s · L5 10 prompts, 0.7 s, "GOLD" as a neutral word | one wrong or one too slow | total time |
| 7 | **Which Was First?** | Three boxes light up almost together. Press the number of the one that lit FIRST. | Gap between boxes: L1 300 ms · L2 200 · L3 120 · L4 80 · L5 50 ms with 4 boxes (keys 1–4) | wrong, or no answer in 4 s | reaction time |
| 8 | **Quick Math** | Two sums. Press **1** if the LEFT is bigger, **2** if the RIGHT. 3 in a row. | L1 3 rounds, single-digit + · L2 4 rounds, + and − · L3 4 rounds, × · L4 5 rounds, two-digit, 4 s each · L5 5 rounds, mixed, 3 s each | one wrong or too slow | total time |
| 9 | **Arrow Rush** | Press the arrows in order with WASD, fast. A GOLD arrow means press the OPPOSITE direction. | L1 4 arrows · L2 6 · L3 6 with 1 gold · L4 8 with 2 gold · L5 10 with 4 gold | wrong key, or 10 s | total time |
| 10 | **Dodge** | Move your dot with WASD. Don't get hit for 8 seconds. | L1 4 slow blocks from above · L2 6 · L3 8, faster · L4 10 from two sides · L5 14 from all four sides. Same storm for everyone (from the seed). | hit | narrowest escape (bigger margin is better) |
| 11 | **Type It** | Type the word and press Enter. A typo and you're out. | L1 4-letter word · L2 6 · L3 8 · L4 two words · L5 three words. Backspace allowed, it just costs time. | submitted text differs from the word, or 10 s | time to Enter |

### 4.3 Mouse — grids (reuse `PuzzleGridView`)

| # | Game | Rule shown on screen | Difficulty ramp | Out when | Rank by |
|---|---|---|---|---|---|
| 12 | **Memory Boxes** | Remember the lit boxes. When they go dark, click them all. One wrong box and you're out. | L1 4×4, 3 boxes, 2.5 s · L2 4 boxes, 2 s · L3 5×5, 5 boxes · L4 6 boxes, 1.5 s · L5 7 boxes, 1.2 s · then +1 box, −0.1 s per level | wrong box, or 10 s | time to finish |
| 13 | **Simon** | Watch the boxes flash in order. Click them back in the same order. One wrong click and you're out. | L1 3×3, 3 steps · L2 4 steps · L3 5 steps, faster · L4 6 steps · L5 4×4, 6 steps · then +1 step per level | wrong click, or 12 s | time to finish |
| 14 | **Spot the Change** | Look at the pattern. It blinks — one box changed. Click it. | L1 4×4, 6 lit, 2 s look · L2 5×5, 8 lit · L3 10 lit, 1.2 s · L4 6×6, 12 lit · L5 14 lit, 0.8 s | wrong box, or 8 s | time |
| 15 | **Mirror** | One box is lit. Click the box that mirrors it across the gold line. | L1 5×5 vertical line · L2 horizontal · L3 diagonal · L4 7×7, 2 lit boxes (click both) · L5 7×7 diagonal, 3 boxes | wrong box, or 8 s | time |
| 16 | **Odd One Out** | One box is different. Click it. | L1 3×3, clearly different colour · L2 4×4, 30 % shade difference · L3 5×5, 20 % · L4 6×6, 12 % · L5 7×7, 8 % | wrong box, or 8 s | time |
| 17 | **Sort It** | Click the numbers from smallest to largest. | L1 4 numbers 1–20 · L2 5 numbers · L3 6 numbers up to 99 · L4 negatives · L5 7 numbers with decimals · then +1 | wrong click, or 12 s | time |
| 18 | **Count the Dots** | Dots flash for a moment. How many were there? | L1 3–6 dots, 1.5 s · L2 4–8, 1 s · L3 5–10, 0.8 s, spread out · L4 some dots moving, 0.6 s · L5 up to 10, 0.4 s | wrong number (answer on the existing 1–10 grid) | time |

### 4.4 Mouse — free pointer

| # | Game | Rule shown on screen | Difficulty ramp | Out when | Rank by |
|---|---|---|---|---|---|
| 19 | **Bullseye** | A target appears somewhere. Click as close to its centre as you can — before it vanishes. | L1 200 px, 2 s · L2 140 px, 1.5 s · L3 100 px, 1.2 s · L4 80 px, drifting · L5 60 px, 0.8 s, drifting | click outside the target, or no click | distance from centre |
| 20 | **Steady Hand** | Keep your cursor inside the gold circle for 5 seconds. Leave it and you're out. | L1 220 px, still · L2 160 px, slow drift · L3 120 px, faster · L4 100 px, shrinking · L5 80 px, jumps every 0.8 s | cursor leaves the circle | closest brush with the edge (bigger margin is better) |

Every game's level content — which boxes light, the bust time, the storm of blocks, the words — comes from the shared seed, so all players face the *same* challenge on the same level. That is what makes "I lost to Ayşe" mean something.

---

## 5. Implementation structure — adding a game must never touch the game flow

### 5.1 New folder: `Assets/_Smartest/Scripts/Minigames/`

```
Minigames/
  Core/
    IMinigame.cs            level parameters + deterministic content generation (pure C#, testable)
    MinigameView.cs         abstract Panel-based view: Setup(level, rng) → Begin(startTime) → Finished(failed, metric)
    MinigameStage.cs        the ONE shared panel: title, rule line, "LEVEL 3" badge, alive chips, countdown, view slot
    EliminationLadder.cs    pure C#: Apply(levelResults) → who's out / repeat / winner; FinalRanking(); TieGroups()
    PayoutTable.cs          pure C#: Deltas(ranking, config)
    LevelRng.cs             System.Random seeded from (gameId, seed, level) — identical on every client
    MinigameRegistry.cs     the list of games
    UiKit.cs                runtime factories (cell, button, bar, dot) using the existing sprite + Palette
  Games/
    GreenLightGame.cs  TimingBarGame.cs  StopTheClockGame.cs  KeepTheBeatGame.cs  PressYourLuckGame.cs
    StroopGame.cs  WhichWasFirstGame.cs  QuickMathGame.cs  ArrowRushGame.cs  DodgeGame.cs  TypeItGame.cs
    MemoryBoxesGame.cs  SimonGame.cs  SpotTheChangeGame.cs  MirrorGame.cs  OddOneOutGame.cs  SortItGame.cs
    CountTheDotsGame.cs  BullseyeGame.cs  SteadyHandGame.cs
```

One file per game holds both its level table and its view. The stage panel is the only object SceneBuilder creates for minigames; each game builds its own contents at runtime with `UiKit`, from the same rounded sprite and palette as everything else — so a new game cannot accidentally look different.

### 5.2 Adding a minigame later

1. Create `Games/MyGame.cs` — subclass `MinigameView`, fill in the level table, implement `Setup`, `Begin`, and call `Finish(failed, metric)`.
2. Add one line to `MinigameRegistry.All`.
3. Run **Tools ▸ Smartest ▸ Build Scenes** (it creates the definition asset from the registry).

Deck, ladder, HUD, level-result beat, ranking screen, payout, scoreboard and tests pick it up automatically. `GameState`, `GameUI`, `RoundDeck` and `SceneBuilder` are not edited.

### 5.3 One-time changes to existing code

| File | Change |
|---|---|
| `RoundTypes.cs` | `RoundKind { Social, Minigame }`; drop `InputType.Choice`; drop resolver types 24–29. |
| `RoundDefinition.cs` | `kind`, `minigameId`; drop the grid/study/trick fields. |
| `RoundCatalog.cs` | Rules rewritten (section 2), 5 strengthened payoffs, 3 new specs, removals. |
| `Resolvers.cs` | Remove Insurance, Taxes, MemorySquares, SymmetryBreak, SimonSequence, TrickSplit; edit Rate/TakeTheHit/Trolley/OneUp/Charity; add Pairs, Sacrifice, Bandwagon. |
| `RoundDeck.cs` | Two decks; `NextAlternating()`; `GameConfig.alternateSocialAndMinigame` (default on). |
| `GameState.cs` | New phases `Play` and `LevelResult`; NetworkVariables `Seed`, `AliveMask` (`SubRound` = level); `ServerBeginLevel / ServerEndLevel` call the ladder; Scoring for minigames reads `PayoutTable`. Remove insurance and the Study phase. |
| `PlayerData.cs` | `SubmitResultRpc(int metric, bool failed)`; `InMinigame` flag for the UI. Remove `Insured`. |
| `GameUI.cs` | `Play` → `MinigameStage.ShowLevel(...)`; `LevelResult` → stage shows who's out; Reveal passes rank chips. |
| `RoundPanel.cs` / `RevealPanel.cs` | Remove choice buttons and the grid path; add the 1ST/2ND/OUT chip style. |
| `GameConfig.cs` | Payout fields (section 3), `levelResultSeconds = 1.5`, `minigameIntroSeconds = 2.5`. |
| `SceneBuilder.cs` | Builds the stage panel; stops building the trick bank and the puzzle grid inside the round panel. |
| `VoiceLines.cs` | Keys: `MinigameStart`, `LevelUp`, `YouAreOut`, `MinigameWinner`. |
| Delete | `TrickQuestions.cs`, `TrickQuestionTests.cs`, the round assets `C06`, `C23`–`C28` (and `C29`–`C32` / `Resources/TrickQuestions.asset` if Build Scenes has created them), the grid-only parts of `PuzzleGenTests.cs` that no longer apply. |

### 5.4 Tests

`ResolverTests.cs` is regenerated from the oracle for the changed and new payoffs. New pure-C# EditMode tests: `EliminationLadderTests` (fail → out; nobody fails → worst out; all fail → repeat harder; one left → winner; ranking order; tie groups; the 10-level cap), `PayoutTableTests` (2–8 players and solo, matching the table in section 3), `LevelRngTests` (same seed + level → identical content), and per-game generator tests (Mirror has exactly one correct answer; Sort It numbers are distinct; Simon never repeats a box twice in a row; Quick Math never produces an equal pair).

### 5.5 Build order (each step compiles and is playable on its own)

1. **Content** — rule rewrite, removals, 5 strengthened rounds, 3 new rounds, tests regenerated. The game is immediately playable as a social-only game with short rules.
2. **Framework** — ladder + payout + tests, GameState/PlayerData additions, stage panel, deck alternation, and *one* game (Green Light) end to end.
3. **Keyboard games** — 2 to 11.
4. **Mouse games** — 12 to 20.
5. **Polish** — voice keys, SceneBuilder assets, `SMARTEST_STATUS.md` updated.

---

## 6. Open questions — answer inline, or just say "go" and I'll use the recommendation

1. **The Button.** With your rule exactly as written ("Reds +15, Greens −5"), Red is always the better choice and nobody would ever press Green. I kept a third clause — "All Red: everyone loses 10" — so betraying still carries a risk. Keep it? *(Recommendation: keep.)*
2. **Ties inside a level.** Players who go out on the same level are ordered by that level's time/accuracy; only an *identical* metric triggers a tie-break level between them. Or should every shared place always play a tie-break level? *(Recommendation: metric first — it's deterministic, never random, and keeps minigames under a minute.)*
3. **"Nobody fails → the slowest is out."** This guarantees every level removes someone and every minigame ends with one winner. OK? *(Recommendation: yes.)*
4. **5–8 players.** 4th–7th get 0 and only last place pays −5. Or should the middle places get small amounts, e.g. `{ 20, 12, 8, 4, 2 }`? *(Recommendation: keep it simple: 20/10/5, last −5.)*
5. **Press Your Luck live feed.** Showing "Mert banked 14" chips while the level is running is a lot of fun and turns it into a nerve contest — but it's the one game where other players' actions are visible mid-level. Include it? *(Recommendation: include.)*
6. **Removal list.** Still Watching?, Honest Friends, Insurance, Taxes, and all trick questions go. Confirm.

Nothing in the Menu, Lobby, Scoreboard, Winner screen, networking or art changes.
