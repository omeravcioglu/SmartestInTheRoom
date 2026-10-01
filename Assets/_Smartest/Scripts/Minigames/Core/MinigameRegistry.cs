using System;
using System.Collections.Generic;

namespace Smartest.Minigames
{
    /// <summary>One minigame, as data. Everything the rest of the game needs to know about it.</summary>
    public sealed class MinigameEntry
    {
        /// <summary>Stable string id. Feeds the level RNG, so changing it changes every level.</summary>
        public string Id;
        /// <summary>Round id for the generated RoundDefinition asset. Keep these unique.</summary>
        public int RoundId;
        public string Title;
        /// <summary>One short line under the title.</summary>
        public string Prompt;
        /// <summary>THE rule. One sentence. A player has 2-4 seconds to read it.</summary>
        public string Rule;
        /// <summary>The MinigameView subclass that draws and plays it.</summary>
        public Type ViewType;
        /// <summary>Which direction of the reported metric is good.</summary>
        public MetricOrder Order = MetricOrder.LowerIsBetter;
        /// <summary>Hard deadline for one level, in seconds. Anyone who hasn't finished by then fails.</summary>
        public float LevelSeconds = 12f;
        /// <summary>Short reminder of the controls, shown in the corner while playing.</summary>
        public string Controls = "";
    }

    /// <summary>
    /// Every minigame in the game. Adding one is: write Games/MyGame.cs, add a line here,
    /// run Tools > Smartest > Build Scenes. Nothing else in the project changes.
    /// </summary>
    public static class MinigameRegistry
    {
        public static readonly List<MinigameEntry> All = new List<MinigameEntry>
        {
            // ---------------- Space ----------------
            new MinigameEntry {
                Id = "green_light", RoundId = 101, Title = "Green Light",
                Prompt = "Don't move until it's green.",
                Rule = "Press SPACE the moment the box turns green. Press early and you're out. Slowest goes out.",
                Controls = "SPACE", ViewType = typeof(GreenLightGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "timing_bar", RoundId = 102, Title = "Timing Bar",
                Prompt = "Stop it in the gold.",
                Rule = "Press SPACE while the marker is inside the gold zone — closer to the middle is better. Miss it and you're out.",
                Controls = "SPACE", ViewType = typeof(TimingBarGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 10f },

            new MinigameEntry {
                Id = "stop_clock", RoundId = 103, Title = "Stop the Clock",
                Prompt = "Count it in your head.",
                Rule = "Stop the clock as close to the target as you can. The numbers hide early. Furthest off goes out.",
                Controls = "SPACE", ViewType = typeof(StopTheClockGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "keep_beat", RoundId = 104, Title = "Keep the Beat",
                Prompt = "The beat stops. You don't.",
                Rule = "Four beats play, then go silent. Keep pressing SPACE in time. Worst timing goes out.",
                Controls = "SPACE", ViewType = typeof(KeepTheBeatGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 16f },

            new MinigameEntry {
                Id = "press_luck", RoundId = 105, Title = "Press Your Luck",
                Prompt = "How long can you hold?",
                Rule = "The bank climbs. Press SPACE to keep it before it busts at a hidden moment. Bust, or bank the least, and you're out.",
                Controls = "SPACE", ViewType = typeof(PressYourLuckGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 14f },

            // ---------------- Number keys / WASD / typing ----------------
            new MinigameEntry {
                Id = "stroop", RoundId = 106, Title = "Stroop",
                Prompt = "Read the colour, not the word.",
                Rule = "Press 1 for RED ink, 2 for BLACK ink — the colour it's printed in, not the word. One mistake and you're out.",
                Controls = "1 / 2", ViewType = typeof(StroopGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "which_first", RoundId = 107, Title = "Which Was First?",
                Prompt = "They light almost together.",
                Rule = "Press the number of the box that lit up FIRST. Wrong box and you're out.",
                Controls = "NUMBER KEYS", ViewType = typeof(WhichWasFirstGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 10f },

            new MinigameEntry {
                Id = "quick_math", RoundId = 108, Title = "Quick Math",
                Prompt = "Bigger side wins.",
                Rule = "Press 1 if the LEFT side is bigger, 2 if the RIGHT side is. One wrong answer and you're out.",
                Controls = "1 / 2", ViewType = typeof(QuickMathGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 20f },

            new MinigameEntry {
                Id = "arrow_rush", RoundId = 109, Title = "Arrow Rush",
                Prompt = "Follow the arrows. Fast.",
                Rule = "Press the arrows in order with WASD. A GOLD arrow means press the OPPOSITE way. One slip and you're out.",
                Controls = "W A S D", ViewType = typeof(ArrowRushGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "dodge", RoundId = 110, Title = "Dodge",
                Prompt = "Same storm for everyone.",
                Rule = "Move with WASD and don't get hit until time runs out. Get hit, or cut it closest, and you're out.",
                Controls = "W A S D", ViewType = typeof(DodgeGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 9f },

            new MinigameEntry {
                Id = "type_it", RoundId = 111, Title = "Type It",
                Prompt = "Spelling counts.",
                Rule = "Type the word exactly and press ENTER. A typo and you're out. Slowest goes out.",
                Controls = "TYPE + ENTER", ViewType = typeof(TypeItGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            // ---------------- Mouse: grids ----------------
            new MinigameEntry {
                Id = "memory_boxes", RoundId = 112, Title = "Memory Boxes",
                Prompt = "Remember the lit boxes.",
                Rule = "The lit boxes go dark. Click every one of them back. One wrong box and you're out.",
                Controls = "CLICK", ViewType = typeof(MemoryBoxesGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 16f },

            new MinigameEntry {
                Id = "simon", RoundId = 113, Title = "Simon",
                Prompt = "Order matters.",
                Rule = "Watch the boxes flash, then click them back in the SAME order. One wrong click and you're out.",
                Controls = "CLICK", ViewType = typeof(SimonGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            new MinigameEntry {
                Id = "spot_change", RoundId = 114, Title = "Spot the Change",
                Prompt = "One box moved.",
                Rule = "The pattern blinks and one box jumps to a new spot. Click where it landed. Wrong box and you're out.",
                Controls = "CLICK", ViewType = typeof(SpotTheChangeGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "mirror", RoundId = 115, Title = "Mirror",
                Prompt = "Across the gold line.",
                Rule = "Click the box that mirrors the lit one across the gold line. Wrong box and you're out.",
                Controls = "CLICK", ViewType = typeof(MirrorGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "odd_one_out", RoundId = 116, Title = "Odd One Out",
                Prompt = "One is a slightly different colour.",
                Rule = "Click the box whose colour is different. Wrong box and you're out.",
                Controls = "CLICK", ViewType = typeof(OddOneOutGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "sort_it", RoundId = 117, Title = "Sort It",
                Prompt = "Smallest to largest.",
                Rule = "Click the numbers in order, smallest first. One wrong click and you're out.",
                Controls = "CLICK", ViewType = typeof(SortItGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            new MinigameEntry {
                Id = "count_dots", RoundId = 118, Title = "Count the Dots",
                Prompt = "No time to count properly.",
                Rule = "The dots flash for a moment. Click how many there were. Wrong number and you're out.",
                Controls = "CLICK", ViewType = typeof(CountTheDotsGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            // ---------------- Mouse: free pointer ----------------
            new MinigameEntry {
                Id = "bullseye", RoundId = 119, Title = "Bullseye",
                Prompt = "Dead centre.",
                Rule = "Click as close to the centre of the target as you can before it vanishes. Furthest off goes out.",
                Controls = "CLICK", ViewType = typeof(BullseyeGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 10f },

            new MinigameEntry {
                Id = "steady_hand", RoundId = 120, Title = "Steady Hand",
                Prompt = "Don't leave the circle.",
                Rule = "Keep your cursor inside the gold circle until time runs out — if it jumps, follow it. Leave it and you're out.",
                Controls = "MOUSE", ViewType = typeof(SteadyHandGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 6f },

            new MinigameEntry {
                Id = "chase", RoundId = 121, Title = "Chase",
                Prompt = "It never stops.",
                Rule = "Keep your cursor on the ball while it moves. Slip off and you're out.",
                Controls = "MOUSE", ViewType = typeof(ChaseGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "buzz_wire", RoundId = 123, Title = "Buzz Wire",
                Prompt = "Don't touch the sides.",
                Rule = "Go from START to the gold end without leaving the path. Touch the edge and you're out.",
                Controls = "MOUSE", ViewType = typeof(BuzzWireGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 16f },

            new MinigameEntry {
                Id = "hover", RoundId = 129, Title = "Hover",
                Prompt = "Hold it in the gold.",
                Rule = "Hold the mouse button to rise, let go to sink. Keep the dot in the gold zone.",
                Controls = "HOLD CLICK", ViewType = typeof(HoverGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "lasers", RoundId = 130, Title = "Lasers",
                Prompt = "Find the gap.",
                Rule = "Keep your cursor in the box and slip through the gap in every red beam.",
                Controls = "MOUSE", ViewType = typeof(LasersGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 16f },

            // ---------------- Mouse: clicking things that move ----------------
            new MinigameEntry {
                Id = "pop", RoundId = 122, Title = "Pop",
                Prompt = "Before they shrink away.",
                Rule = "Click every dot before it vanishes. Let one get away and you're out.",
                Controls = "CLICK", ViewType = typeof(PopGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "shell_game", RoundId = 124, Title = "Shell Game",
                Prompt = "Keep your eye on the ball.",
                Rule = "Watch the ball go under a cup. Follow that cup through the shuffle, then click it.",
                Controls = "CLICK", ViewType = typeof(ShellGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "stack", RoundId = 125, Title = "Stack",
                Prompt = "Build it straight.",
                Rule = "Click to drop each sliding block onto the tower. Miss the tower and you're out.",
                Controls = "CLICK", ViewType = typeof(StackGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            new MinigameEntry {
                Id = "keepy_uppy", RoundId = 126, Title = "Keepy Uppy",
                Prompt = "Don't let it drop.",
                Rule = "Click the ball to bounce it up. If it touches the floor, you're out.",
                Controls = "CLICK", ViewType = typeof(KeepyUppyGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "flap", RoundId = 127, Title = "Flap",
                Prompt = "Mind the gaps.",
                Rule = "Click to flap and fly through the gaps. Touch anything and you're out.",
                Controls = "CLICK", ViewType = typeof(FlapGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 15f },

            new MinigameEntry {
                Id = "flash_point", RoundId = 128, Title = "Flash Point",
                Prompt = "Blink and it's gone.",
                Rule = "A dot flashes and disappears. Click exactly where it was.",
                Controls = "CLICK", ViewType = typeof(FlashPointGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 10f },

            // ---------------- Catching ----------------
            new MinigameEntry {
                Id = "catch", RoundId = 131, Title = "Catch",
                Prompt = "Eyes up, basket down.",
                Rule = "Move the basket with your mouse and catch every falling ball. Miss one and you're out.",
                Controls = "MOUSE", ViewType = typeof(CatchGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 13f },

            new MinigameEntry {
                Id = "good_catch", RoundId = 132, Title = "Good Catch",
                Prompt = "Gold in, red out.",
                Rule = "Catch the gold, let the red fall past. Miss a gold or catch a red and you're out.",
                Controls = "MOUSE", ViewType = typeof(GoodCatchGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 15f },

            new MinigameEntry {
                Id = "goalie", RoundId = 133, Title = "Goalie",
                Prompt = "Nothing gets past.",
                Rule = "Move your glove up and down to stop every shot. Let one in and you're out.",
                Controls = "MOUSE", ViewType = typeof(GoalieGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 13f },

            new MinigameEntry {
                Id = "fireflies", RoundId = 134, Title = "Fireflies",
                Prompt = "Catch them by touch.",
                Rule = "Touch every firefly with your cursor before time runs out. They won't sit still.",
                Controls = "MOUSE", ViewType = typeof(FirefliesGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 11f },

            new MinigameEntry {
                Id = "runaway", RoundId = 135, Title = "Runaway",
                Prompt = "It doesn't want to be caught.",
                Rule = "The dot runs from your cursor. Corner it and click it before time runs out.",
                Controls = "CLICK", ViewType = typeof(RunawayGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "toss", RoundId = 136, Title = "Toss",
                Prompt = "Get them in the air.",
                Rule = "Things fly up from below. Click each one before it falls back down.",
                Controls = "CLICK", ViewType = typeof(TossGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 15f },

            // ---------------- Memory, Simon-style ----------------
            new MinigameEntry {
                Id = "chimp", RoundId = 137, Title = "Chimp Test",
                Prompt = "Remember where they were.",
                Rule = "Click the numbers from 1 up. After your first click the rest hide, so remember where they were.",
                Controls = "CLICK", ViewType = typeof(ChimpGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 16f },

            new MinigameEntry {
                Id = "pairs", RoundId = 138, Title = "Pairs",
                Prompt = "Look quick, match them all.",
                Rule = "The cards show for a moment. Then flip two at a time and match every pair.",
                Controls = "CLICK", ViewType = typeof(PairsGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 20f },

            new MinigameEntry {
                Id = "echo", RoundId = 139, Title = "Echo",
                Prompt = "Say it back in colours.",
                Rule = "Watch the colours light up, then click them back in the same order.",
                Controls = "CLICK", ViewType = typeof(EchoGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            new MinigameEntry {
                Id = "backwards", RoundId = 146, Title = "Backwards",
                Prompt = "Simon, the other way round.",
                Rule = "Watch the boxes flash, then click them back in REVERSE order. One wrong click and you're out.",
                Controls = "CLICK", ViewType = typeof(BackwardsGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            // ---------------- Mouse: stay on it ----------------
            new MinigameEntry {
                Id = "decoy", RoundId = 140, Title = "Decoy",
                Prompt = "Only yours.",
                Rule = "Stay on your ball. Look-alikes cross its path; don't follow them.",
                Controls = "MOUSE", ViewType = typeof(DecoyGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "ring", RoundId = 141, Title = "Ring",
                Prompt = "It breathes.",
                Rule = "Keep your cursor inside the gold ring as it grows and shrinks.",
                Controls = "MOUSE", ViewType = typeof(RingGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "road", RoundId = 142, Title = "Road",
                Prompt = "Mind the bends.",
                Rule = "Steer with your mouse and keep the dot on the winding road.",
                Controls = "MOUSE", ViewType = typeof(RoadGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "spotlight", RoundId = 143, Title = "Spotlight",
                Prompt = "Lights out.",
                Rule = "It's dark and your cursor is a torch. Find the gold dot and click it.",
                Controls = "CLICK", ViewType = typeof(SpotlightGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "tag", RoundId = 144, Title = "Tag",
                Prompt = "You're not it.",
                Rule = "The red dot chases your cursor. Keep away from it and stay in the box.",
                Controls = "MOUSE", ViewType = typeof(TagGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "pour", RoundId = 145, Title = "Pour",
                Prompt = "Stop on the line.",
                Rule = "Hold the mouse button to pour. Let go on the line. Spill over and you're out.",
                Controls = "HOLD CLICK", ViewType = typeof(PourGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 10f },

            // ---------------- Reflexes ----------------
            new MinigameEntry {
                Id = "quick_draw", RoundId = 147, Title = "Quick Draw",
                Prompt = "Wait for it.",
                Rule = "Wait for the gold target, then click it fast. Click before it shows, or hit a red one, and you're out.",
                Controls = "CLICK", ViewType = typeof(QuickDrawGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 20f },

            new MinigameEntry {
                Id = "twins", RoundId = 148, Title = "Twins",
                Prompt = "Two in a row.",
                Rule = "Cards flip one by one. Click the moment one matches the card before it. Don't click on a non-match.",
                Controls = "CLICK", ViewType = typeof(TwinsGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 16f },

            new MinigameEntry {
                Id = "whack", RoundId = 149, Title = "Whack",
                Prompt = "Gold ones only.",
                Rule = "Moles pop up. Click the gold ones before they duck. Never click a red one.",
                Controls = "CLICK", ViewType = typeof(WhackGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "ruler_drop", RoundId = 150, Title = "Ruler Drop",
                Prompt = "Catch it quick.",
                Rule = "Click the instant the ruler drops. The less it falls, the better. Click early and you're out.",
                Controls = "CLICK", ViewType = typeof(RulerDropGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            new MinigameEntry {
                Id = "let_go", RoundId = 151, Title = "Let Go",
                Prompt = "Hold on. Hold on. Now.",
                Rule = "Hold the mouse button. Let go the instant the circle turns red. Too early and you're out.",
                Controls = "HOLD CLICK", ViewType = typeof(LetGoGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            new MinigameEntry {
                Id = "statues", RoundId = 152, Title = "Statues",
                Prompt = "Red light, green light.",
                Rule = "Hold the button to run, let go to freeze. Run on green, freeze on red. Move on red and you're out.",
                Controls = "HOLD CLICK", ViewType = typeof(StatuesGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 17f },

            new MinigameEntry {
                Id = "slice", RoundId = 153, Title = "Slice",
                Prompt = "Slash the gold.",
                Rule = "Hold the button and slash through the gold before it falls. Never slice a red bomb.",
                Controls = "HOLD CLICK", ViewType = typeof(SliceGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            new MinigameEntry {
                Id = "colour_match", RoundId = 154, Title = "Colour Match",
                Prompt = "See it, click it.",
                Rule = "A colour shows in the middle. Click the pad of the same colour, fast. Wrong pad and you're out.",
                Controls = "CLICK", ViewType = typeof(ColourMatchGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            // ---------------- Skill: flick, aim, steer ----------------
            new MinigameEntry {
                Id = "juggle", RoundId = 155, Title = "Juggle",
                Prompt = "Two balls. Then three.",
                Rule = "Click a ball to bat it up. They knock into each other. Let one touch the red floor and you're out.",
                Controls = "CLICK", ViewType = typeof(JuggleGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 12f },

            new MinigameEntry {
                Id = "fishing", RoundId = 156, Title = "Fishing",
                Prompt = "Gold yes. Red no.",
                Rule = "The hook follows your mouse. Put it on a gold fish and click to reel it in. Touch a red pufferfish and you're out.",
                Controls = "MOUSE + CLICK", ViewType = typeof(FishingGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "hoops", RoundId = 157, Title = "Hoops",
                Prompt = "Pull back. Let fly.",
                Rule = "Press on the ball, pull back and let go to shoot. Sink enough baskets before your shots run out.",
                Controls = "DRAG", ViewType = typeof(HoopsGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 17f },

            new MinigameEntry {
                Id = "putt", RoundId = 158, Title = "Putt",
                Prompt = "Mind the windmill.",
                Rule = "Press on the ball, pull back and let go. Sink it within the strokes; hit it too hard and it skips the hole.",
                Controls = "DRAG", ViewType = typeof(PuttGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 17f },

            new MinigameEntry {
                Id = "darts", RoundId = 159, Title = "Darts",
                Prompt = "Hold your breath.",
                Rule = "Your aim sways. Hold the button to steady it, let go to throw, but don't hold too long. Miss the board and you're out.",
                Controls = "HOLD CLICK", ViewType = typeof(DartsGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 13f },

            new MinigameEntry {
                Id = "maze", RoundId = 163, Title = "Maze",
                Prompt = "Don't touch the walls.",
                Rule = "Take your cursor from START to the gold exit without touching a wall. Later on, the lights go out.",
                Controls = "MOUSE", ViewType = typeof(MazeGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 17f },

            new MinigameEntry {
                Id = "tightrope", RoundId = 164, Title = "Tightrope",
                Prompt = "Don't look down.",
                Rule = "Slide the pole with the mouse, against the lean, to keep the walker up. Watch for gusts. Fall and you're out.",
                Controls = "MOUSE", ViewType = typeof(TightropeGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 10f },

            new MinigameEntry {
                Id = "herd", RoundId = 166, Title = "Herd",
                Prompt = "You're the sheepdog.",
                Rule = "Sheep run from your cursor. Steer every one of them through the gap in the fence and into the pen.",
                Controls = "MOUSE", ViewType = typeof(HerdGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            new MinigameEntry {
                Id = "penalty", RoundId = 168, Title = "Penalty",
                Prompt = "Send him the wrong way.",
                Rule = "The keeper follows your aim. Pull him one way, then click to shoot the other. Score three from five.",
                Controls = "MOUSE + CLICK", ViewType = typeof(PenaltyGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 17f },

            new MinigameEntry {
                Id = "traffic", RoundId = 169, Title = "Traffic",
                Prompt = "No crashes. No road rage.",
                Rule = "Click a car to stop it, click again to wave it on. Don't let two cars meet, or keep one waiting too long.",
                Controls = "CLICK", ViewType = typeof(TrafficGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 18f },

            // ---------------- Memory and timing, the second batch ----------------
            new MinigameEntry {
                Id = "trail", RoundId = 160, Title = "Trail",
                Prompt = "Follow the snake.",
                Rule = "Watch the path crawl across the grid, then draw it back in order. One wrong cell and you're out.",
                Controls = "DRAG / CLICK", ViewType = typeof(TrailGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 15f },

            new MinigameEntry {
                Id = "repaint", RoundId = 161, Title = "Repaint",
                Prompt = "Paint it from memory.",
                Rule = "A picture shows for a moment, then it's wiped. Pick colours from the palette and paint it back exactly.",
                Controls = "CLICK / DRAG", ViewType = typeof(RepaintGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 20f },

            new MinigameEntry {
                Id = "whats_missing", RoundId = 162, Title = "What's Missing?",
                Prompt = "One of them walked off.",
                Rule = "Remember the tray. It blinks and comes back one short. Click the one that went. Wrong pick and you're out.",
                Controls = "CLICK", ViewType = typeof(WhatsMissingGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 11f },

            new MinigameEntry {
                Id = "pop_lock", RoundId = 165, Title = "Pop the Lock",
                Prompt = "Click on the gold.",
                Rule = "Click (or SPACE) as the needle crosses the gold notch. Click early, or let it slip past, and you're out.",
                Controls = "CLICK / SPACE", ViewType = typeof(LockGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "rhythm", RoundId = 167, Title = "Rhythm",
                Prompt = "Hit it on the line.",
                Rule = "Press A, S or D (or click the lane) as each note crosses the line. Miss one, or hit nothing, and you're out.",
                Controls = "A / S / D / CLICK", ViewType = typeof(RhythmGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 12f },
        };

        public static MinigameEntry Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        public static MinigameEntry ByRoundId(int roundId)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].RoundId == roundId) return All[i];
            return null;
        }
    }
}
