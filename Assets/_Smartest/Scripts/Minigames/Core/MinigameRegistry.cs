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
                Rule = "Press SPACE while the marker is inside the gold zone. Miss the zone and you're out.",
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
                Rule = "The bank climbs. Press SPACE to keep it. It busts at a hidden moment — bust and you get nothing.",
                Controls = "SPACE", ViewType = typeof(PressYourLuckGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 14f },

            // ---------------- Number keys / WASD / typing ----------------
            new MinigameEntry {
                Id = "stroop", RoundId = 106, Title = "Stroop",
                Prompt = "Read the colour, not the word.",
                Rule = "Press 1 for RED ink, 2 for GREEN ink — the colour it's printed in, not the word. One mistake and you're out.",
                Controls = "1 / 2", ViewType = typeof(StroopGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "which_first", RoundId = 107, Title = "Which Was First?",
                Prompt = "They light almost together.",
                Rule = "Press the number of the box that lit up FIRST. Wrong box and you're out.",
                Controls = "1 / 2 / 3", ViewType = typeof(WhichWasFirstGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 10f },

            new MinigameEntry {
                Id = "quick_math", RoundId = 108, Title = "Quick Math",
                Prompt = "Bigger side wins.",
                Rule = "Press 1 if the LEFT sum is bigger, 2 if the RIGHT one is. One wrong answer and you're out.",
                Controls = "1 / 2", ViewType = typeof(QuickMathGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 16f },

            new MinigameEntry {
                Id = "arrow_rush", RoundId = 109, Title = "Arrow Rush",
                Prompt = "Follow the arrows. Fast.",
                Rule = "Press the arrows in order with WASD. A GOLD arrow means press the OPPOSITE way. One slip and you're out.",
                Controls = "W A S D", ViewType = typeof(ArrowRushGame),
                Order = MetricOrder.LowerIsBetter, LevelSeconds = 14f },

            new MinigameEntry {
                Id = "dodge", RoundId = 110, Title = "Dodge",
                Prompt = "Same storm for everyone.",
                Rule = "Move with WASD and don't get hit. Survive the whole level or you're out.",
                Controls = "W A S D", ViewType = typeof(DodgeGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 14f },

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
                Rule = "The pattern blinks and one box changes. Click the one that changed. Wrong box and you're out.",
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
                Rule = "Keep your cursor inside the gold circle until time runs out. Leave it once and you're out.",
                Controls = "MOUSE", ViewType = typeof(SteadyHandGame),
                Order = MetricOrder.HigherIsBetter, LevelSeconds = 14f },
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
