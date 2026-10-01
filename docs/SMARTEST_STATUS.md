# Smartest in the Room — status

**Unity 6000.0.32f1 · URP · 2D/Canvas only · `E:\UnityProjects\SmartestInTheRoom`**
First to 100 points wins. 1–8 players online. No in-game chat (Discord). All code lives under
`Assets/_Smartest/`, namespace `Smartest`.

---

## What the game is now

**A match is minigames only**, one after another, until somebody reaches 100.

**Minigames (69; the full list with every rule is `MINIGAMES.md`).** Everyone plays the same level simultaneously. Fail and you're out of that
minigame. Levels 1–2 are warm-ups: clear one and you play on. From level 3, if nobody fails,
the slowest goes. If everyone fails, the level comes back harder. Last one standing wins. 1st +20, 2nd +10, 3rd +5, last −5 — all three numbers live on
GameConfig.

**Question rounds (20) — switched off, 29 Sep.** Playtesters found the Red/Green questions not
fun, so `GameConfig.questionRounds` is off and the deck deals none. Nothing was deleted: the
rounds, their resolvers, assets, UI and tests are all still in place, and switching it back on
brings them back (dealt between minigames, `minigamesPerQuestion` to one question). For the
record: everyone answers the same question at once, the payoff depends on what the room did,
and five of them only work with three or more players.

Flow per challenge:

```
social    RoundIntro -> Answering -> Reveal -> Scoring
minigame  RoundIntro -> (Play -> LevelResult)* -> Reveal -> Scoring
```

Nothing after Scoring changed: the 100-point check, the tie-break, the scoreboard and the
winner screen are the same as before.

## Plan: more minigames (29 Sep)

With the questions gone, minigames are the whole game, so two things matter: more games of the
kinds playtesters liked, and more sense of playing *together* (today everyone plays the same
level side by side and only sees who's done or out).

**A. More of what works: no new tech, each game one file. Done 29 Sep: fifteen games, see
*Fifteen more minigames* below.** Trace and Orbit were dropped for Herd and Pop the Lock
(Orbit was too close to Chase and Ring); Tightrope became a walker you balance instead of a
ball on a beam; What's Missing asks *which* one went, not where.

**B. See each other (needs a small networking addition).** A live position stream (a few updates
a second per player) so the race games show everyone's marker: Buzz Wire, Road, Statues, Chase
become visible races, and the last two play a head-to-head.

**C. Shared-arena games (needs the host to simulate a small world from players' inputs).**
Bumper Dots (push each other off the edge), King of the Hill (hold the shrinking gold zone),
Hot Potato (pass the bomb by touching someone). The biggest step, and the most party-like.

**Alongside:** after each playtest, tune the ramps and retire what isn't fun; the reading-heavy
ones (Stroop, Quick Math, Type It) are the first candidates.

## Architecture

| Area | Where | Notes |
|---|---|---|
| Match engine | `Scripts/Rounds/GameState.cs` | Host-authoritative NetworkBehaviour, spawned from `Resources/GameState.prefab` once every client has loaded the Game scene. Clients only react to networked field changes. |
| Social payoffs | `Scripts/Rounds/Resolvers.cs` + `RoundCatalog.cs` | One resolver per rule; rules and numbers are data. |
| Minigame rules | `Scripts/Minigames/Core/EliminationLadder.cs`, `PayoutTable.cs` | Pure C#, no Unity, fully unit-tested. |
| Minigames | `Scripts/Minigames/Games/*.cs` | One file each. Level content comes from `LevelRng.For(gameId, seed, level)`, so every client builds the identical level. |
| Minigame UI | `Scripts/Minigames/Core/MinigameStage.cs` + `UiKit.cs` + `KitWatch.cs` | One stage hosts every game (rule card, level column, game panel, banners); games draw only through UiKit, which draws in the Tabloid style. |
| UI kit | `Scripts/UI/Kit`, `Scripts/UI/Hud` | The Tabloid skin: generated art (`InkSprites`), builders (`Ink`), masthead, race to 100, seat rail, host caption. See *Tabloid UI* below. |
| Networking | `Scripts/Net/NetSession.cs` | Netcode for GameObjects 2.13.2 + Multiplayer Services 2.3.1 (Relay sessions), plus LAN and 127.0.0.1 modes. |
| Everything generated | `Editor/SceneBuilder.cs` | **Tools ▸ Smartest ▸ Build Scenes** rebuilds folders, the Ink art, font assets, config, challenge assets, prefabs and both scenes. Manual scene edits are overwritten. Also runs headless: `-executeMethod Smartest.EditorTools.SceneBuilder.BuildScenesBatch`. |

**What goes over the wire during a minigame:** the seed, the level number, who is playing, and
one result per player per level (`SubmitLevelResultRpc`). The host never simulates a game.
Timings are measured on each player's own machine, so ping never costs a reaction; the phase
clock is Netcode server time, so "GO" lands on the same instant everywhere.

## Adding a minigame

1. `Scripts/Minigames/Games/MyGame.cs` — subclass `MinigameView`, fill in the level table,
   implement `Build()`, react in `OnTick()`, call `Finish(failed, metric)` once.
2. One line in `MinigameRegistry.All`, and its intro line in `VoiceScript.MinigameIntros` (a
   test insists; until the clip is recorded in the Voice Studio it's simply silent).
3. **Tools ▸ Smartest ▸ Build Scenes**.

`MinigameSmokeTests` (PlayMode) then plays levels 1–12 of it, as a player and as a spectator,
and fails if any level throws or never ends.

The deck, ladder, HUD, level-result beat, ranking screen, payout and tests pick it up
automatically. `GameState`, `GameUI`, `RoundDeck` and `SceneBuilder` are not touched.

## Tuning without code

`Assets/_Smartest/Data/GameConfig.asset`:
`minigamePlacePoints` (default 20/10/5), `minigameLastPlacePoints` (−5),
`minigameSoloClearPoints`, `minigameSoloLevels`, `minigameIntroSeconds`,
`minigameLeadInSeconds`, `levelResultSeconds`, `minigameMaxLevels`,
`minigameWarmUpLevels` (2; 0 cuts the slowest from level one), `questionRounds` (off),
`alternateSocialAndMinigame` and `minigamesPerQuestion` (both only matter with questions on),
plus the existing round timings and `targetScore`.

## Next time you open Unity

1. Let it compile.
2. **Tools ▸ Smartest ▸ Clean Up Retired Files** — the retired code is already gone; this now
   only offers the 16 unused pre-rewrite voice files (`<key>.mp3` without a number).
3. **Tools ▸ Smartest ▸ Build Scenes** — keeps every generated file in step with the code (the
   89 challenge assets, prefabs and both scenes). A new minigame is only dealt once this has
   made its asset (last run 29 Sep, after the 15 new ones).
4. **Window ▸ General ▸ Test Runner** — EditMode ▸ Run All (159) and PlayMode ▸ Run All.
5. Play from `Menu.unity`. Two instances (or two PCs) to see a real elimination.
6. To ship: **Tools ▸ Smartest ▸ Build Windows Release** (see *Publishing* below).

## Still unverified

- Only the solo host loop has been run end to end (PlayMode `MatchFlowTests`: host a Local
  lobby, start, round one deals, back to lobby, start again). Nothing with two machines yet.
- UGS Relay/Lobby enablement for this project. `GameConfig.networkMode = Lan` or `Local`
  avoids the cloud entirely if Relay misbehaves.
- Minigame feel. Every level ramp is a first draft, meant to be tuned by playing. Hiding the
  level until GO (see below) made the memory and flash games noticeably harder than before.

150 of the 165 voice lines are recorded and wired. The 15 intros for the 29 Sep minigames are
written (`VoiceScript.MinigameIntros`) but not recorded, so those games start silently until
they are; recording spends ElevenLabs credits. New lines
record from Tools ▸ Smartest ▸ Voice Studio ▸ Record missing, or without opening Unity:
`Unity.exe -batchmode -projectPath . -executeMethod Smartest.EditorTools.VoiceStudioWindow.RecordMissingBatch -quit`
(same saved key, voice and settings; records only what's missing, then wires it in).

## Known gaps and deliberate omissions

- **Press Your Luck has no live "X banked" feed.** Showing other players' banks mid-level
  would need per-frame networking, which nothing else in the game does. The fuse is shared,
  so the nerve contest is still real, but you can't watch someone else chicken out.
- **Sort It uses whole numbers only.** The design doc mentioned decimals; it ramps with more
  numbers and negatives instead, which is the same skill and easier to read.
- Grid puzzles are now drawn at runtime by each minigame rather than by the old shared
  `PuzzleGridView`, which is why that component is retired.

## Publishing

**Tools ▸ Smartest ▸ Build Windows Release** (`Editor/ReleaseBuilder.cs`; headless:
`-executeMethod Smartest.EditorTools.ReleaseBuilder.BuildWindowsBatch -quit`) checks the project
first and refuses to build if: the build scenes aren't exactly Menu then Game, a registered
minigame has no round asset (run Build Scenes), or a font licence file is missing. It warns
about a default company name and unrecorded voice lines. Then it builds a non-development
player into a new folder, `Build/Release/SmartestInTheRoom-<version>-win64/` (never over an
older one), moves Burst's debug symbols out to `Build/Release/_symbols/`, and writes a README.
Every standalone build, this one or File ▸ Build, gets a `Licenses/` folder with the four SIL
OFL texts (Archivo, Atkinson Hyperlegible Next and Mono, Liberation Sans), which the font
licence asks for.

**Release candidate, 29 Sep:** `Build/Release/SmartestInTheRoom-0.1.0-win64/` (116.5 MB, Mono,
all 69 minigames). It launches cleanly; nobody has played it on two machines yet, which is what
it's for. An earlier build from the same evening is under `Build/Release/_superseded/`, and the old
dev build is still loose in `Build/`. It predates the HUD and art pass on the 49 newer minigames
(started later the same evening): once that has landed and Build Scenes has run, build again.

Done 29 Sep: product name "Smartest in the Room" (it was "SmartestInTheRoom"); the template's
`SampleScene` taken out of the build, and Build Scenes no longer keeps scenes from outside
`_Smartest`; the front page no longer advertises question rounds while they're off (a
Minigames box and a Knockout box instead).

Still open before a public release:
- **Two machines.** Nothing has been played across two PCs yet; only the solo host loop is
  tested. Play a full match with 3–4 people, over Relay and over LAN.
- **Unity Cloud.** Relay/Lobby must be enabled for this project in the Unity Cloud dashboard,
  and the project linked, or online hosting fails (LAN and same-PC modes don't need it).
- **Company name** (Player settings; still DefaultCompany) and a **version number** for the
  release (0.1.0 now).
- **ElevenLabs licence.** A paid ElevenLabs plan is what allows commercial use of the voice.
  Check the plan the clips were made on before selling the game.
- **Icon and store art.** The build uses Unity's default icon; Steam and itch.io need a capsule
  image and screenshots.
- **Playtest tuning** of the new games' ramps, and a design pass on anything that isn't fun.
- **Version control.** The project isn't in git; put it in before release so a build can be
  traced to its code.

## Fifteen more minigames — 29 Sep 2026

Plan A, built. Each is still one file, but these reach further than the earlier ones: the first
drag-and-release games, real physics, a maze generator, a crowd to steer.

| Game | What you do | Out when | Ranked by |
|---|---|---|---|
| Juggle | Click balls to bat them up: two, then three, and they knock into each other | a ball touches the floor | closest any ball came to the floor |
| Fishing | The hook follows the mouse; hook gold fish and click to reel them in, weave round red pufferfish | a pufferfish touches the hook, or time | time to land the catch |
| Hoops | Slingshot: press on the ball, pull back, let go. The guide dots shorten each level; rim and backboard bounces; the hoop drifts from level 3 | out of shots | time |
| Putt | Crazy golf, same slingshot. Bumpers, a windmill bar from level 4; too fast and it skips the hole | out of strokes (3) | strokes, then time |
| Darts | The aim sways; hold to steady it (a held breath), let go to throw. Hold too long and you shake | a dart off the board | total distance from the bull |
| Maze | Cursor from START to the gold exit through a freshly carved maze; dark from level 6, the cursor a torch | touching a wall | time |
| Tightrope | Slide the balance pole against the lean to keep a walker up; gusts are signposted a moment before | falling | average lean |
| Herd | Be the sheepdog: sheep flee the cursor and flock; get them all through the gap into the pen; a pond from level 5 | time | time |
| Penalty | The keeper follows your aim a beat late and dives when you shoot: pull him one way, shoot the other. Three from five | three goals out of reach | shots, then time |
| Traffic | Click cars at a crossroads to stop them and wave them on; two roads, then three, then four | a crash, or a car kept waiting (road rage) | total waiting caused |
| Trail | A path crawls across the grid and hides; draw it back, dragging or clicking | a wrong cell | time |
| Repaint | A picture of coloured cells shows and is wiped; paint it back from a palette | time | time |
| What's Missing? | A tray of shapes blinks and comes back one short (shuffled from level 4); pick the missing one of four | a wrong pick | time |
| Pop the Lock | Click as the needle crosses the gold notch; it turns round after every hit | clicking off the notch, or letting it pass | total distance from the notches' middles |
| Rhythm | Notes fall down three lanes; hit A, S or D (or click the lane) on the line. Off-beats, then chords | a missed note, or a hit on nothing | total timing error |

How some of them work: the maze is a random depth-first carve (one way through, plenty of dead
ends), and the whole stroke since the last frame is checked, so a flick can't jump a wall.
Tightrope is an inverted pendulum stepped at 1/240 s; the walker stays put and the rope
scrolls. Herd's sheep flee, flock and wander, with fences as segments they slide along.
Traffic and Fishing draw inside a `RectMask2D`, so things slide in from the edges. Rhythm reads
keys through the new `KeyInput.LanesPressed()`, a bitmask, so a chord's two keys in one frame
both count. New in the kit: `UiKit.Line` / `SetLine` (a rotated thin bar), used for walls, the
rope, fences, the lock's notch and the net. Someone knocked out watches the level played for
them: the walker balances, the dog herds, the cop waves traffic through, the keeper gets
feinted, the hoops and putts are taken.

Tightrope is the one ramp that isn't a guess: its first draft was near impossible, so its
numbers were tuned against a simulated player with a quarter-second reaction (a sloppy one
clears level 1 about nine times in ten; by level 8 only a steady one does). Every other ramp
is still a first draft. Also fixed while rendering them: the rule card's controls row counts
DRAG as mouse, and a game you can play either way (Rhythm, Pop the Lock) says "Mouse or
keyboard" rather than one or the other.

## More minigames, fewer questions — 28 Sep 2026

After a playtest: the questions were too much reading to understand and decide in time, and
Simon and Steady Hand were the favourites. So a match now deals two minigames per question,
and there are ten new minigames in their spirit: visual, mostly mouse, one line of rules.

| Game | What you do | Out when | Ranked by |
|---|---|---|---|
| Chase | Keep the cursor on a ball that never stops | you slip off | average distance from its centre |
| Pop | Click each dot before it shrinks away | one gets away | total reaction time |
| Buzz Wire | Cursor from START to the gold end along a winding path | you touch the edge | time from START |
| Shell Game | Follow the ball's cup through the shuffle, click it | wrong cup | time to answer |
| Stack | Click to drop sliding blocks; the overhang is cut off | you miss the tower | total width cut away |
| Keepy Uppy | Click the ball to keep it off the floor | it touches the floor | closest it came to the floor |
| Flap | Click to flap through the gaps | you hit anything | narrowest scrape |
| Flash Point | A dot flashes; click where it was (two in a row from level 6) | too far off | distance off |
| Hover | Hold the button to rise; stay in the moving gold zone | out of it for a moment | average distance from its middle |
| Lasers | Slip the cursor through the gap in every red beam | a beam touches you | narrowest escape |

Then, asked for at least 30 games of the Simon / catching / keep-the-mouse-on-it kind (15
existed), sixteen more:

| Game | What you do | Out when | Ranked by |
|---|---|---|---|
| Catch | Basket follows the mouse; catch every falling ball | one drops | average distance from the basket's middle |
| Good Catch | Catch, with red balls that must fall past | a gold drops or a red is caught | same |
| Goalie | Glove follows the mouse up and down; stop every shot | one goes in | average distance from the glove's middle |
| Fireflies | Touch every wandering firefly with the cursor (they shy away from level 6) | time runs out | time |
| Runaway | A dot flees the cursor; corner it and click it (it tires) | it gets away | time |
| Toss | Things are thrown up in arcs; click each before it lands | one lands | total reaction time |
| Chimp Test | Numbers hide after you click the 1; click the rest in order from memory | wrong order | time |
| Pairs | Cards show for a moment, then match every pair | time runs out | time |
| Echo | Classic four-colour Simon: play the sequence back | wrong colour | time |
| Backwards | Simon played back last-first | wrong box | time |
| Decoy | Stay on your ball while identical ones cross it | you follow the wrong one | average distance |
| Ring | Keep the cursor in a ring that swells, shrinks and drifts | you leave the band | average distance from the band's middle |
| Road | Steer a car along a winding road you can see coming | off the road | average distance from the middle |
| Spotlight | The cursor is a torch in the dark; find and click the gold dot | wrong dot, or time | time |
| Tag | Red dots chase the cursor round a box | tagged, or you leave the box | closest they got |
| Pour | Hold to pour, let go on the line (surges and settling later) | spilled, or too far off | distance from the line |

Spotlight's torch is a round UI `Mask` that follows the cursor over a scene held still behind
it. Echo's pads are plain boxes rather than kit cells, so its red and green are just colours.
Backwards is `SimonGame` with a `Backwards` switch.

And eight reflex games:

| Game | What you do | Out when | Ranked by |
|---|---|---|---|
| Quick Draw | Wait, then click the gold target the instant it appears (red decoys from level 4) | clicking early, a red, or too slow | total reaction time |
| Twins | Cards flip; click when one matches the card before (colours shuffled from level 4) | clicking a non-match, or missing a pair | total reaction time |
| Whack | Whack-a-mole on a 3×3 board; gold yes, red never | a gold ducks unwhacked, or a red is hit | total reaction time |
| Ruler Drop | Click the instant the ruler falls (bait twitches from level 4) | clicking early, or it falls through | total distance fallen |
| Let Go | Hold the button, release the instant the circle turns red (blue baits from level 3) | letting go early, or too slow | total reaction time |
| Statues | Red light, green light: hold to run, let go to freeze | still holding once STOP's grace is up | time to the finish |
| Slice | Hold and slash through the gold before it lands; bombs ride along | a gold lands, or a bomb is sliced | total reaction time |
| Colour Match | Click the pad matching the colour in the middle (pads swap from level 4) | wrong pad, or too slow | total reaction time |

Signals never rely on red against green alone: Let Go and Statues say NOW / GO / STOP in words,
Twins puts a number on every card, and Colour Match uses gold, blue, red and ink, which differ
in brightness too. Every level's slowest possible run fits inside its deadline.

Every level is built from the shared seed, so everyone plays the same one. The physics games
(Keepy Uppy, Flap, Hover) step in fixed 1/240 s steps, so frame rate doesn't change the ball.
Level 1 of each is slow and forgiving. Past the first few levels the ramps stop at a floor
someone can still clear (the lesson of Timing Bar and Stroop). The games that start from your
cursor wait for you: Chase until you're on the ball, Lasers until you're in the
box, Buzz Wire until you reach START, Flap until your first click, Hover until your first
press. So nobody is out because of where their mouse happened to be at GO. Like every ramp
here, the numbers are a first draft to be tuned by playing.

Also new: `KeyInput.MouseHeld`, `UiKit.Fill` (a flat rectangle, for paths and bands drawn in
pieces), and `MinigameView.Advance(dt)` (what Update calls; the smoke test drives it directly).

## Tabloid UI — 27 Sep 2026

The whole interface was rebuilt to the **Tabloid** design: newsprint paper, one ink, one gold,
headlines with a gold drop, stickers and stamps. Red and green only ever mean an answer (red
carries a diamond, green a circle); **gold is points gained, blue is points lost**.

**Screens.** Game: a masthead (round + kind, *the race to 100*, sound), the stage (round,
minigame or reveal), the host's caption bubble, and a seat rail in join order. Winner: a full
front page. Menu: front page with a play card, join card, lobby with a seat grid, sound modal
(now with a *Host captions* switch).

**Where it lives.**

| Piece | File |
|---|---|
| Tokens (old role names kept as aliases, so minigames didn't change) | `Scripts/Core/Palette.cs` |
| Type roles, font set | `Scripts/Core/Typo.cs`, `FontSet.cs` |
| Every shape, drawn in code, baked to `Resources/Ink` by Build Scenes | `Scripts/UI/Kit/InkSprites.cs` |
| Builders: boxes, stickers, stamps, keycaps, tokens, headlines | `Scripts/UI/Kit/Ink.cs` |
| Hard shadow, headline drop, point marks, timer burst, slab hover | `HardShadow`, `TextDrop`, `TextMarks`, `Burst`, `SlabPress` |
| Masthead, race to 100, seat rail and card, caption, sound button | `Scripts/UI/Hud/*` |

Each screen builds itself in a static `Create(...)` next to its own logic; SceneBuilder only
assembles them. Layouts are written in the design's own coordinates on a 1920 × 1080 *Frame*
inside an Expand-mode canvas, so 16:10 or ultrawide screens get more paper around the page
instead of a page that runs off the edge.

**Minigames.** UiKit draws the new look and `KitWatch` translates the colours games already
set: a red cell gets ✕, a green one ✓, the mirror line gets the gold hatch, gold/red/green game
words go on a chip. Only Type It changed (its target word uses the mono face). The content
area is 1200 × 520. The lead-in still hides the level until GO (the board showed it faintly;
that would bring back the free-look bug).

**Networking additions.** `PlayerData.LevelDone` (seat says DONE while others play) and
`PlayerData.OutAtLevel` (OUT stamp, "out · level 2"), set by `GameState`.
`VoiceLines.LineStarted(text, seconds)` feeds the caption bubble.

**Fonts.** SceneBuilder turns any of these TTFs found in `Assets/_Smartest/Fonts` into TMP
assets (dynamic: glyphs, Turkish included, come from the TTF on demand; LiberationSans is the
fallback for the few symbols they lack — the build log lists them) and lists them in
`Resources/SmartestFonts`: `Archivo_ExtraCondensed-Black`, `Archivo-Black`,
`Archivo_Condensed-ExtraBold`, `Archivo_Expanded-ExtraBold`, `AtkinsonHyperlegibleNext-Regular`
/ `-Bold` / `-Italic` / `-BoldItalic`, `AtkinsonHyperlegibleMono-ExtraBold` (the static files
from the Google Fonts downloads). All nine are installed, with their SIL OFL licences beside
them — ship those with the game. Take them out and everything falls back to TMP's default
face, which is much wider: headlines shrink to fit and the round timer steps down onto the
answer gap when a long question reaches it.

**Seeing it without playing.** `Tests/PlayMode/ScreenshotTour.cs` ([Explicit]) plays a solo
Local match and saves a PNG of every screen, every registered minigame included (while the first
one is live it starts each of the others on the same stage, then restarts the dealt level), then
stages what solo can't reach (a full rail, someone going out, watching, a dead heat, a tie-break):
`Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testFilter ScreenshotTour`
(pictures in `Previews/`, or `$SMARTEST_SHOTS`).

**Removed:** `ScoreboardUI`, `PlayerRowView`, `RevealRowView`, `CountdownBar` and the
`PlayerRow` / `RevealRow` prefabs (a copy of everything before the rebuild is in
`Backups/pre-tabloid-20260927-152413`). *Clean Up Retired Files* also offers the old
`Sprites/RoundedPanel.png`.

## Review pass — 27 Sep 2026

**Bugs fixed**

- **No sound at all.** Neither scene (nor the Bootstrap) had an AudioListener — SceneBuilder
  builds the cameras without one — so every voice line, effect and music loop played to
  nobody. `AudioDirector` now adds one to the persistent Bootstrap; SceneBuilder puts it on
  the prefab.
- *Back to lobby → Start* never began a second match: the engine was spawned with Netcode's
  default `destroyWithScene: false`, survived into the Menu in the Winner phase, and the next
  Start found it "already spawned". Now spawned with `destroyWithScene: true`.
- The game paused when its window lost focus (`runInBackground` was off): an alt-tabbed host
  froze everyone, and two local instances stalled each other. `GameBootstrap` forces it on.
- Minigame levels were visible during the 3-2-1 — two free seconds in the memory and flash
  games (Count the Dots' 0.25 s flash was really 2.25 s), and Bullseye could be aimed before
  the clock started. `MinigameStage` now reveals the content at GO.
- LiberationSans has no ▶ ◀ ✓, so Arrow Rush's RIGHT and LEFT were the same empty box, and the
  lock-in tick was a box too. Now ► ◄ √.
- Keep the Beat knocked you out for tapping along with the lead beats; those taps are now free
  and the lead beats click.
- Steady Hand from level 5 was impossible: the circle jumped away with no time to follow.
  Each jump now gives a moment to get back in.
- Floors nobody could clear, which fed endless "everyone failed, again, harder" loops: Timing
  Bar (19 ms window), Stroop (0.5 s per word), Which Was First (30 ms gaps).
- Quick Math's per-prompt allowances added up to more than its deadline.
- Everyone sharing one place (a stalemate at the level cap) were all "last" and all got −5.
- A play-off whose other player left made the survivor replay alone until the level cap.
- Green Light / Which Was First timed reactions from the scheduled instant, not the frame the
  light appeared; a press on that very frame now counts as early.
- Count the Dots: dots could overlap beyond counting; "moving" dots never moved.
- Dodge spectators were "HIT" by a dot they don't control; Sort It failed a double-click.
- Metrics that tied constantly (each tie is a play-off): Bullseye, Steady Hand, Press Your Luck.
- Voice: "Two left!" played on every level of a two-player game; "That's everybody, no
  takebacks" fired at the reveal whether or not everybody answered.
- Reveal chips said YES/NO for PULL/WAIT, VOLUNTEER/STAY QUIET, DONATE/KEEP, GIVE/KEEP.
- The winner screen said "SMARTEST IN THE GROUP".
- During minigames every scoreboard row said "thinking"; it now says playing / waiting / out.
- Rounds with a 0 button lay out 0–5 over 6–10; the 0 used to sit alone on a third row, over
  the "Locked in" hint.
- Every locked-in answer was sent to every machine while the others were still choosing
  (`PlayerData.CurrentAnswer` was readable by all). No screen showed it, but anyone looking at
  the traffic could. Now only the host and the player themselves receive it; everyone else
  gets the answers at the reveal, as before.
- When a player left, the questions that need three (Lowest Unique, Two Thirds, The Pot,
  Charity, Sus) could still be dealt to the two left. The deck now drops them.
- If everyone else left mid-match, the host kept playing alone to 100. The match now ends and
  the host goes back to the lobby, where the hint line says "Everyone else left, so the match
  ended." until someone joins. A match started solo still plays on, and the winner screen
  stays up if people leave after the match is decided.
- A stalemate at the level cap that left several players sharing first named only one of
  them as the winner.

**Rule changes** (each one failed the "real decision" test above; tests updated)

| Round | Was | Now |
|---|---|---|
| The Snap | Balanced: Reds +10, Greens −10; otherwise nothing — Red never lost, so nobody should press Green | Exactly half pick Red: Reds vanish (0), Greens +10. Any other split: everyone −5. The sub-line shows the exact count for the room |
| Pick a Pill | Red pays if *fewer than* half pick it — with two players it never could | *Half or fewer* |
| The Door | A player who didn't answer counted as a climber and sank the real one | Only a pressed Green climbs |
| The Pot | Putting in 0 was always your best answer (each point comes back as 2/N), and contributions were capped at your score — at 0-0-0 the round did nothing | If the pot comes to under 3 per player, everyone also loses 5 — someone has to feed it, and how much depends on the room. No score cap |
| Silent Auction | No 0 button, so the only way to stay out was to not answer (yet a "nobody bid" line and test existed) | 0 is a bid: "Bid 0 to sit out" |
| Sus | A non-answer counted as Green and could win | Only pressed colours count; one colour for all pays nobody |
| Charity | A donor tied for last gave to nobody — at 0-0-0 every donation vanished | It goes to the others tied for last with them |

Deadlines now match the games: Dodge 9 s, Steady Hand 6 s, Quick Math 20 s.

**Minigame changes**

- **Warm-up levels.** On levels 1–2 only failing knocks you out; "slowest goes out" starts at
  level 3. Before, a two-player minigame was settled on level one, the easiest, by whoever was
  a few milliseconds slower, and nobody ever saw the rest of the ramp. If everyone clears a
  warm-up, the level result says so and says when the cuts start.
- **Stroop** uses red and black ink instead of red and green. About one man in twelve can't
  tell red from green, so for them it was the one minigame they could never win.

**Removed** — they passed on paper but didn't make sense in play:

- *Rule One, Rate This Game, Is This a Dream?, Pairs, Bandwagon* — team rounds. The room
  agrees on Discord and everybody scores the same, so nothing is decided between players.
- *Attack the Leader* — attacking never helped your own score; it only cost you 3 to hurt
  someone else.
- *The Lever* — on the last pull, pulling was always the best answer.

**Added** — each one checked by brute force for every player count and every combination of
the others' answers: others always change your result, no answer is always best, and there is
no deal the room can make that nobody profits from breaking.

| Round | Rule | Why it's played together |
|---|---|---|
| Undercut (C29) | Score your number, doubled if someone picked exactly one above you. If someone picked exactly one below you, you score nothing. | No safe agreement exists: if everyone takes 10, a 9 scores 18. Pure read-the-room |
| Gold Rush (C30) | Red digs: the diggers split 10. Green sells shovels: +6 for every digger. | Digging alone is the best seat, but a second digger halves the gold and doubles the shovel money — the room has to settle who digs |
| Mirror Match (C31) | Your mirror is 11 minus your number. Score your number if someone picked your mirror — unless someone else picked yours too. | Symmetry you need a partner for: 10 pairs with 1, 6 with 5, so every pair splits 11 unevenly and someone takes the small end |

The multi-step round support (`subRounds`, `ContinueSubRounds`) now has no round using it.

## Removed in the multiplayer-first redesign

Still Watching? (no decision), Honest Friends (everyone picks 1), Insurance (no other player
can affect it), Taxes (the answer changes nothing), and all four trick-question rounds plus
the 20-question bank (other players could only change the size of your prize, never whether
you won it). Remember the Squares, Symmetry and Simon came back as elimination minigames.

Strengthened because one option was always at least as good as the others: Rate This Game
(now needs an average of exactly 7 — later removed, see the review), Take the Hit (a lone
volunteer is now paid), Trolley (nobody pulling now costs 10), 1-Up (a lone holdout is now
paid), Charity (donors profit once half the room joins in).
