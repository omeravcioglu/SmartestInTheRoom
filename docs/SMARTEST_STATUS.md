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

**Give it a demo.** The rule card plays level 1 of the game by itself (`Demo` is true,
`Interactive` false) with a hand showing the moves, and `EveryMinigameDemoShowsAMoveAndWins`
fails a game whose demo shows nothing in its first 7 s, or loses. In the not-Interactive path,
play the level the way a good player would through the same methods the input drives (guard
those with `CanMove`, keep reading the mouse and keys under `CanAct`), and report the moves:
`PointAt`, `HoldAt`, `TapAt`, `PressKey` (`DemoAnswer` for a key that answers a question), with
`Where(piece)` for positions. Show the input the
rule text names (a click when it says click). Anything that would solve the level for a
knocked-out player watching it (a sequence, the odd one out) goes behind `Demo`. If level 1
hides what the game is about, override `DemoLevel`. See *How-to-play demos* below.

Drawing it: the kit's boxes, dots and cells for anything abstract; for something a player
would recognise (a cup, a fish, a mole) add a piece to `Scripts/UI/Kit/InkArt.Pieces.cs` (one
shape per line, canvas pixels, y down) and place it with `UiKit.Art`. Build Scenes bakes it.
Report progress with `Progress(...)` / `Tries(...)` and the stage shows the scoreline tabs.

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
4. **Window ▸ General ▸ Test Runner** — EditMode ▸ Run All (161) and PlayMode ▸ Run All.
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
about a default company name, a missing app icon and unrecorded voice lines. Then it builds a non-development
player into a new folder, `Build/Release/SmartestInTheRoom-<version>-win64/` (never over an
older one), moves Burst's debug symbols out to `Build/Release/_symbols/`, and writes a README.
Every standalone build, this one or File ▸ Build, gets a `Licenses/` folder with the four SIL
OFL texts (Archivo, Atkinson Hyperlegible Next and Mono, Liberation Sans), which the font
licence asks for.

**Release candidate, 2 Oct:** `Build/Release/SmartestInTheRoom-0.1.0-win64/` (119.1 MB, Mono,
all 69 minigames with the illustrated art, the how-to-play demos and the trimmed menus, the app
icon). Built after Build Scenes, EditMode 161/161 and PlayMode (smoke, demos, match flow) on the
real project; it launches cleanly. It still hasn't been played across two PCs, which is what it's
for. Older candidates are under `Build/Release/_superseded/`, and the old dev build is still loose in `Build/`.

Checked 1 Oct, on the real project: EditMode 159/159; PlayMode MatchFlowTests and
MinigameSmokeTests (all 69 games, levels 1-12, as player and spectator); the full ScreenshotTour
(82 screens, every game looked at); and **online hosting**: the explicit `OnlineHostingCheck`
test signs in to Unity Services and opens a real private Relay lobby, which got a join code. So
the project is linked and Relay and the session service are on; online play needs nothing more
in the Unity Cloud dashboard. Run that test again before each release.

Done 29 Sep - 1 Oct:
- Product name "Smartest in the Room" (it was "SmartestInTheRoom"); the template's `SampleScene`
  out of the build, and Build Scenes no longer keeps scenes from outside `_Smartest`; the front
  page no longer advertises question rounds while they're off (Minigames and Knockout boxes).
- **VSync on** for the PC quality level. Standalone builds had VSync off and no frame cap, so the
  menus ran the GPU flat out.
- **An app icon**: `Art/AppIcon.png`, a gold sticker with an ink S and the leader's star, set with
  Tools ▸ Smartest ▸ Use App Icon. A placeholder in the game's style; swap the PNG and run the
  menu item again. The release check warns if there's no icon.
- **The finished stamp** ("OUT", "DONE — WAITING") moved to the top of the game panel. It used to
  sit on the line where every game says why the level ended for you ("TOO SLOW") and hid it.
- **Fewer words around the menus.** Gone: the front page's top strip ("1–8 PLAYERS · ONLINE, WI-FI
  OR SAME PC", "FIRST TO 100 WINS"), "No in-game chat. Talk on Discord." (front page and lobby),
  the lobby's ONLINE / WI-FI / LOCAL sticker and the line explaining it, "1 OF 8 SEATS TAKEN" (the
  1/8 counter says it), the host's start hint, the join card's IP / LOCAL hint ("CODE OR IP" is now
  "JOIN CODE"), and the sound panel's "No music yet" note. Joining by IP or LOCAL still works; the
  menus just don't advertise it. The build's README.txt still explains it. `12-front-page.png` was
  re-rendered to match.
- **Store screenshots** in `Steam/Screenshots/` (12 at 1920 × 1080, 4K masters, alternatives),
  made by the explicit `SteamShots` test after the art pass; see the README there. The release
  candidate above was rebuilt after the art pass too.

Still open before a public release:
- **Rebuild the release candidate**: the 2 Oct one predates the lobby options, kick, match menu
  and screen settings (below).
- **A privacy policy.** Players sign in anonymously to Unity Gaming Services and join through
  Relay; UGS's terms want the game's privacy policy to say so, and Steam's store page links one.
- **Two machines.** Play a full match with 3–4 people on the release candidate, over Relay and
  over LAN. Hosting online is proven; a full match between PCs isn't yet.
- **Company name** (Player settings; still DefaultCompany) and a **version number** for the
  release (0.1.0 now).
- **ElevenLabs licence.** A paid ElevenLabs plan is what allows commercial use of the voice.
  Check the plan the clips were made on before selling the game. 15 intros are unrecorded.
- **Store art.** Screenshots, an icon and a 62-second trailer (`Steam/Trailer/`, made from the
  game's own screens by the explicit `TrailerCapture` test and `Tools/Trailer/build_trailer.py`,
  with original synthesised music; see its README) exist. Steam also needs the capsule images
  (header 920 × 430, small 462 × 174, main 1232 × 706, vertical 748 × 896) and the library images.
- **Playtest tuning** of the ramps, and a design pass on anything that isn't fun.
- **Version control.** The project isn't in git; put it in before release so a build can be
  traced to its code.

Worth adding (found in the 2 Oct check): music (`Audio/Music` is empty: there is none, menus or
matches); Steam friend invites and rich presence (needs Steamworks); a credits screen; the "Made
with Unity" splash is optional on Unity 6 and could go or match the paper look. Worth removing:
the question-round system if it isn't coming back, and unused packages (Visual Scripting, AI
Navigation, Timeline, the Version Control plugin).

## Lobby options, kick, match menu, screen settings — 2 Oct 2026

- **Host options.** The lobby's left column shows MATCH LENGTH (Short, first to 50; Standard,
  100; Long, 150) and MINIGAMES (how many of the 69 are in the deck), with CHOOSE opening the
  minigame list (`MinigamePicker`: a ticket per game, gold when in, struck through when out; at
  least 3 stay in). Only the host can change them; guests see them. `HostOptions` holds the
  choice and saves it on the host's machine (PlayerPrefs `smartest.host_options`: the games
  switched off, so new games start in). `MatchSettings`, a network object the host spawns with
  the lobby (Resources/MatchSettings, kept for the whole session), shows it to everyone; the
  match plays to `MatchSettings.Target` (the race track, seat rail, scoring and winner page read
  it) and GameState deals only the games left on.
- **Kick.** The host gets a small ✕ on every other seat in the lobby; one click turns it into a
  red KICK?, a second sends the player away with "The host removed you from the lobby." on their
  front page. The lobby remembers their install (`NetSession.DeviceId`, sent with the name on
  arrival) and turns them away if they come back, until the host opens a new lobby. Lobby only.
- **Match menu.** Esc, or the ☰ beside the sound button, opens it mid-match: RESUME, SETTINGS,
  and the way out. A guest's LEAVE MATCH goes to the front page while the others play on; the
  host's END MATCH takes everyone back to the lobby ("The host ended the match.") and LEAVE closes
  the lobby. The ways out ask twice. The match keeps running underneath.
- **Menus hold the game's input.** While the match menu or the settings are open, `KeyInput`
  reports no game keys or clicks (`KeyInput.Hold`), so a click on a menu button never lands in a
  minigame underneath. Clicks on the old sound panel used to.
- **Screen settings.** Settings is two columns now: sound on the left; on the right FULL SCREEN
  (always the monitor's own resolution) or WINDOW with a size from the 16:9 ones that fit, and
  host captions. Unity remembers the screen mode itself. The front page has a SETTINGS link next
  to QUIT.
- Tests: `HostOptionsTests` (EditMode: lengths, the minimum, saving, junk saves, window sizes);
  `MatchFlowTests` gained the options shaping a match, no kick on your own seat, the picker, END
  MATCH and LEAVE from the menu. Kicking needs a second player, so it's down for the two-machine
  playtest.

## How-to-play demos — 1 Oct 2026

Playtesters said the games start before anyone understands them: the rule card was up for 2.5 s.
It's now up for 8 s (`minigameIntroSeconds`), and its right half is a **HOW TO PLAY** screen:
the game's own level 1, scaled down, playing itself on a loop, with a mouse pointer that glides,
clicks (a gold splash) and holds (a gold dot), and keys that pop up in the screen's corner while
the matching keycap in the controls row dips. The title, rule and controls stay on the left; the
prizes became one row of chips under the demo.

How it works:
- `MinigameView` has `Demo`, set before `Prepare`. A demo is never Interactive, its sounds are
  hushed (`Sounds.Hush`), and nothing it does reaches the host. Games report their moves with
  `PointAt` / `HoldAt` / `TapAt` / `PressKey` (`DemoHand`, `DemoTapped`, `DemoKeyPressed`), and
  shared action methods use `CanMove` (CanAct, or a demo) while input reading stays on `CanAct`.
  A quiz answers with `DemoAnswer`, which shows the key 0.45 s before the game takes it, so the
  key pops up beside the question it answers, not the next one. A demo never steps more than
  50 ms at once: after a slow frame it runs slow instead of missing its cue.
- `MinigameStage` builds a fresh view for every run of the demo, seeded `0x5EED + run` (never
  the match's seed, so the demo never previews the real level), lets a run play out, holds the
  finished picture 1.2 s and starts the next; the demo screen ignores real clicks.
- Every game's demo plays its level the way a good player would and wins it: Simon and Echo
  play the sequence back, Herd's pointer is the dog, Maze finds the way out with a search over
  the walls, Slice swings a real blade through the oranges, Penalty feints the keeper, Pop the
  Lock clicks on the gold as the needle gets there, Type It types the word, the space-bar games
  press SPACE on cue. Stroop's demo plays level 2 (`DemoLevel`): level 1 never prints a word in
  the other colour, so it can't show the trap. Play that would give an answer away is behind
  `Demo`, so a knocked-out player watching the real level never sees it solved; motion games
  (catching, steering) also play for those watchers, as before.
- The cursor is a new art piece, `cursor`. `RuleCardShots` (an explicit PlayMode test) renders
  each game's card at 1.5, 3.5 and 5.5 s for checking by eye; `$SMARTEST_CARD_ONLY` picks games.
- Fixed on the way, in real play too: `KitWatch` lost a result's words when a game set the same
  colour twice in a row (Penalty's second "GOAL" was green on a green chip), and Dodge's blocks
  crossed the WASD hint on their way out of the arena (now clipped to it).

## Minigame art and HUD — 1 Oct 2026

The 49 minigames added since the first 20 got a HUD and, where they show real things, pictures.

**Scoreline tabs.** Two tabs on the game panel's top edge, drawn by the stage: what you've done
(`Progress("CAUGHT", 3, 7)`: pips up to 10, "n / m" above that, or a bar for "stay in"
games) and what you have left (`Tries("SHOTS", 2, 5)`). Hidden until GO and for spectators.
`Scoreline.cs`; games report through `MinigameView.Progress` / `Tries`.

**Illustrated pieces** (`InkArt.cs` draws them, `InkArt.Pieces.cs` lists them): 33 pieces in
the kit's flat ink-and-paper style, baked to `Resources/Ink/art_*.png` by Build Scenes like
every other shape. A *tinted* piece is white where its colour goes, so games still colour it
with Palette roles (a gold fish turns green when caught). `UiKit.Art` places one, `Dress`
turns a cell into one (Shell Game's cups stay clickable cells), `PivotOn` puts the pivot on a
point of the drawing, `Marker` adds the kit's star / diamond / dot / square / ✕ / ✓, and
`FinishLine` the chequered strip.

Where they went: Shell Game (cups on a table), Catch (a basket; Good Catch's red balls carry a
white diamond), Goalie (glove, net, footballs that spin), Flap (a bird that noses up and
dives), Keepy Uppy and Penalty (a football; the keeper has a face), Juggle (balls), Slice
(oranges that fall apart in two halves along your slash, red bombs, and a blade trail),
Whack (moles and bombs rising out of holes), Pour (a tap and a running stream), Ruler Drop
(finger and thumb that pinch shut), Statues (a runner with two strides, stock still when
frozen, and a chequered finish), Toss (flipping coins), Quick Draw (targets; a red one is
crossed out), Fireflies (glowing flies in the dark), Fishing (fish that face their way and
are hauled up by the mouth, pufferfish, a hook), Hoops (a basketball, a meshed net), Darts
(crosshair, darts), Herd (sheep and a sheepdog), Tag (red faces whose eyes follow you),
Runaway (a worried dot whose eyes look where it runs), Traffic (cars that face their way),
Putt (a pennant), Echo (each pad has its own mark), Pairs (card backs), Road (centre-line
dashes that run past). Red things the rules call red stay red (Slice's and Whack's bombs);
games whose rule says "the dot" (Road, Runaway, Hover) still show a dot.

Checked 1 Oct on the real project: Build Scenes (the 33 pieces baked), EditMode 161/161 (two
new art tests), PlayMode MatchFlowTests and MinigameSmokeTests; and the full ScreenshotTour (82
screens) on a copy. To photograph only some games, set `SMARTEST_TOUR_ONLY=traffic,herd` before
running the tour: it shoots just those and stops.

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
