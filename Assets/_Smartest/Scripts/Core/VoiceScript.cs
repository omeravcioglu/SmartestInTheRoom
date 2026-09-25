using System.Collections.Generic;

namespace Smartest.Core
{
    /// <summary>
    /// Every key the host can speak. One key = one moment in the game; each key has several
    /// written variants so nothing repeats twice in a night.
    /// Clips live at Assets/_Smartest/Audio/Voice/&lt;key&gt;_1.mp3, _2, _3...
    /// </summary>
    public static class VoiceKeys
    {
        // Menu and lobby
        public const string MenuWelcome = "menu_welcome";
        public const string MenuHostPressed = "menu_host_pressed";
        public const string MenuJoinPressed = "menu_join_pressed";
        public const string LobbyPlayerJoined = "lobby_player_joined";
        public const string LobbyFull = "lobby_full";
        public const string LobbySoloStart = "lobby_solo_start";
        public const string GameStart = "game_start";

        // Social rounds
        public const string RoundStart = "round_start";
        public const string TimerTen = "timer_ten";
        public const string TimerLocked = "timer_locked";
        public const string RevealEveryoneSame = "reveal_everyone_same";
        public const string RevealOneWinner = "reveal_one_winner";
        public const string RevealAllLost = "reveal_all_lost";
        public const string RevealSplit = "reveal_split";
        public const string NobodyAnswered = "nobody_answered";

        // Scores
        public const string ScoreBigGain = "score_big_gain";
        public const string ScoreBigLoss = "score_big_loss";
        public const string Comeback = "comeback";
        public const string LeadChange = "lead_change";
        public const string CloseToWin = "close_to_win";
        public const string LastPlace = "last_place";

        // Minigames
        public const string MinigameStart = "minigame_start";
        public const string LevelUp = "level_up";
        public const string MinigameRepeat = "minigame_repeat";
        public const string MinigameTieBreak = "minigame_tiebreak";
        public const string MinigameSurvived = "minigame_survived";
        public const string MinigameClean = "minigame_clean";
        public const string YouAreOut = "you_are_out";
        public const string MinigameLastTwo = "minigame_last_two";
        public const string MinigameWinner = "minigame_winner";

        // End of match
        public const string Winner = "winner";
        public const string WinnerLastPlace = "winner_last_place";

        /// <summary>Per-minigame intro, e.g. "mg_green_light". Built from the registry id.</summary>
        public static string Minigame(string minigameId) => "mg_" + minigameId;
    }

    /// <summary>
    /// The whole script, in one place, so rewriting the host means editing this file and
    /// re-running Tools &gt; Smartest &gt; Voice Studio — no code changes anywhere else.
    ///
    /// Voice: a friend on the couch who is way too into this. Casual, loud, a bit chaotic,
    /// never actually cruel — the joke is always the situation, not the person.
    /// Lines never name a player, because these are static clips.
    /// </summary>
    public static class VoiceScript
    {
        public sealed class Entry
        {
            public string Key;
            public VoiceLines.PlayMode Mode = VoiceLines.PlayMode.Always;
            public float Chance = 1f;
            public bool OncePerSession;
            public float CooldownSec;
            /// <summary>Louder moments interrupt quieter ones. Quiet asides never cut in.</summary>
            public int Priority;
            public string[] Texts;
        }

        private static Entry E(string key, VoiceLines.PlayMode mode, float chance, bool once,
            float cooldown, int priority, params string[] texts)
        {
            return new Entry
            {
                Key = key, Mode = mode, Chance = chance, OncePerSession = once,
                CooldownSec = cooldown, Priority = priority, Texts = texts
            };
        }

        public static List<Entry> All()
        {
            var list = new List<Entry>
            {
                // ---------------- Menu and lobby ----------------
                E(VoiceKeys.MenuWelcome, VoiceLines.PlayMode.OncePerSession, 1f, true, 0f, 1,
                    "Oh hey, you showed up. Alright — let's find out who's actually the smartest here. Spoiler: it's never the one who thinks it is.",
                    "Look who it is. Get your friends in, first to a hundred points, bragging rights forever. Or until next weekend."),

                E(VoiceKeys.MenuHostPressed, VoiceLines.PlayMode.Chance, 0.35f, true, 0f, 0,
                    "Hosting? Bold. Everything that goes wrong is officially your fault now.",
                    "You're the host. That's basically a job. Unpaid."),

                E(VoiceKeys.MenuJoinPressed, VoiceLines.PlayMode.Chance, 0.35f, true, 0f, 0,
                    "Joining someone else's game. Smart. Zero responsibility.",
                    "Ooh, you got invited. Look at you, with friends."),

                E(VoiceKeys.LobbyPlayerJoined, VoiceLines.PlayMode.Chance, 0.55f, false, 6f, 0,
                    "Another one. The betrayals are going to be incredible.",
                    "Hey, somebody else showed up.",
                    "Ooh, more victims.",
                    "That's another one. Keep them coming."),

                E(VoiceKeys.LobbyFull, VoiceLines.PlayMode.Always, 1f, false, 0f, 2,
                    "Eight people. Full house. This is going to get messy and I am so here for it."),

                E(VoiceKeys.LobbySoloStart, VoiceLines.PlayMode.Always, 1f, false, 0f, 2,
                    "Just you? Okay. You're definitely the smartest one here. Also the dumbest. Congratulations."),

                E(VoiceKeys.GameStart, VoiceLines.PlayMode.Always, 1f, false, 0f, 2,
                    "Alright, here we go! First to a hundred. Talk all the trash you want — I can't hear you, but I'll see it in the scores.",
                    "Okay okay okay. Hundred points. Let's find out who folds first."),

                // ---------------- Social rounds ----------------
                E(VoiceKeys.RoundStart, VoiceLines.PlayMode.Chance, 0.28f, false, 30f, 0,
                    "Next one.",
                    "Okay, here we go.",
                    "Alright, this one's fun.",
                    "Ooh, I like this one."),

                E(VoiceKeys.TimerTen, VoiceLines.PlayMode.Chance, 0.5f, false, 45f, 1,
                    "Ten seconds! Pick something!",
                    "Ten seconds, let's go, let's go.",
                    "Clock's running. Come on."),

                E(VoiceKeys.TimerLocked, VoiceLines.PlayMode.Chance, 0.35f, false, 40f, 0,
                    "That's everybody. No takebacks.",
                    "Locked in. Let's see it.",
                    "Alright, hands off."),

                E(VoiceKeys.RevealEveryoneSame, VoiceLines.PlayMode.Chance, 0.6f, false, 0f, 1,
                    "Everybody picked the same thing. You people are so predictable.",
                    "All of you. The exact same answer. Do you share a brain?",
                    "Wow, unanimous. That's either teamwork or you all panicked at once."),

                E(VoiceKeys.RevealOneWinner, VoiceLines.PlayMode.Chance, 0.6f, false, 0f, 1,
                    "One of you read that perfectly. Everyone else — look at what you did.",
                    "Just one. Somebody cooked.",
                    "Oh, that's a solo win. That has to feel good."),

                E(VoiceKeys.RevealAllLost, VoiceLines.PlayMode.Always, 1f, false, 0f, 1,
                    "Everybody lost. All of you. That takes real effort.",
                    "Nobody got anything. Beautiful. Genuinely beautiful.",
                    "Zero for the entire room. I'm not even mad, that's impressive."),

                E(VoiceKeys.RevealSplit, VoiceLines.PlayMode.Chance, 0.45f, false, 25f, 0,
                    "Ooh, split room.",
                    "Half and half. Nobody trusts anybody here."),

                E(VoiceKeys.NobodyAnswered, VoiceLines.PlayMode.Always, 1f, false, 0f, 1,
                    "Nobody answered? Are you all asleep?",
                    "Hello? Anyone? Okay, moving on."),

                // ---------------- Scores ----------------
                E(VoiceKeys.ScoreBigGain, VoiceLines.PlayMode.Chance, 0.5f, false, 25f, 0,
                    "Oh, that's a big one!",
                    "Huge. That is a huge round.",
                    "Somebody's eating well tonight."),

                E(VoiceKeys.ScoreBigLoss, VoiceLines.PlayMode.Chance, 0.5f, false, 25f, 0,
                    "Ouch. That's going to sting.",
                    "Oh no. Oh nooo.",
                    "That hurt to watch."),

                E(VoiceKeys.Comeback, VoiceLines.PlayMode.Chance, 0.7f, false, 40f, 1,
                    "Wait — they were last! Look at this!",
                    "Comeback! Out of absolutely nowhere!",
                    "Hang on. When did that happen?"),

                E(VoiceKeys.LeadChange, VoiceLines.PlayMode.Chance, 0.55f, false, 20f, 0,
                    "New leader! Everyone panic.",
                    "Ooh, lead change.",
                    "And just like that, somebody else is in front.",
                    "There it goes. New name on top.",
                    "We've got a new leader. You should probably gang up on them."),

                E(VoiceKeys.CloseToWin, VoiceLines.PlayMode.Always, 1f, false, 30f, 2,
                    "Somebody is almost at a hundred. You might want to do something about that.",
                    "That's dangerously close. Stop them.",
                    "One good round and this is over. Just saying."),

                E(VoiceKeys.LastPlace, VoiceLines.PlayMode.Chance, 0.4f, false, 45f, 0,
                    "Somebody down there is having a rough night.",
                    "Last place is getting lonely.",
                    "It's not over for last place. It's mostly over. But not totally."),

                // ---------------- Minigames ----------------
                E(VoiceKeys.MinigameStart, VoiceLines.PlayMode.Always, 1f, false, 0f, 1,
                    "Okay! No talking your way out of this one.",
                    "Minigame! Actual skill required, sorry.",
                    "Alright, hands on the keyboard. Let's see it.",
                    "This one you can't fake. Good luck."),

                E(VoiceKeys.LevelUp, VoiceLines.PlayMode.Chance, 0.5f, false, 6f, 0,
                    "Harder.",
                    "Faster now.",
                    "Okay, level up. Good luck.",
                    "It gets worse from here.",
                    "Again, but meaner."),

                E(VoiceKeys.MinigameRepeat, VoiceLines.PlayMode.Always, 1f, false, 0f, 1,
                    "All of you failed? Fine. Again. Harder.",
                    "Nobody made it. That's embarrassing. One more time.",
                    "Wow. Every single one of you. Okay, we're doing that again."),

                E(VoiceKeys.MinigameTieBreak, VoiceLines.PlayMode.Always, 1f, false, 0f, 1,
                    "Dead heat! You two — settle it.",
                    "Exactly the same? No. Do it again, just you.",
                    "We are not flipping a coin. Play it off."),

                E(VoiceKeys.MinigameSurvived, VoiceLines.PlayMode.Chance, 0.4f, false, 8f, 0,
                    "You made it. Barely counts, but you made it.",
                    "Still alive. Nice.",
                    "Survived. Don't get comfortable."),

                E(VoiceKeys.MinigameClean, VoiceLines.PlayMode.Chance, 0.5f, false, 20f, 0,
                    "That was fast. Suspiciously fast.",
                    "Okay, that was actually clean."),

                E(VoiceKeys.YouAreOut, VoiceLines.PlayMode.Always, 1f, false, 0f, 2,
                    "And you're out. Sit down.",
                    "Ohhh, you're done. Enjoy the show.",
                    "That's you gone. Painful.",
                    "Out! You're out. It's fine. It's fine.",
                    "Yeah, you're cooked. Watch how it's done.",
                    "Nope. Out. Next."),

                E(VoiceKeys.MinigameLastTwo, VoiceLines.PlayMode.Always, 1f, false, 0f, 1,
                    "Two left! This is the good part.",
                    "Down to two. Everybody watch.",
                    "Just you two now. No pressure."),

                E(VoiceKeys.MinigameWinner, VoiceLines.PlayMode.Always, 1f, false, 0f, 1,
                    "Last one standing! Twenty points, well earned.",
                    "That's the win. Beat everybody in the room.",
                    "Winner! Absolutely cooked the rest of them.",
                    "Ohh, they took it. Twenty points."),

                // ---------------- End of match ----------------
                E(VoiceKeys.Winner, VoiceLines.PlayMode.Always, 1f, false, 0f, 3,
                    "A HUNDRED! That's the game! Smartest in the room, officially. Put it on a résumé.",
                    "That's a hundred points. It's over. They win. Everybody else — better luck next time.",
                    "And that's the match! Somebody just beat all of you. Live with that."),

                E(VoiceKeys.WinnerLastPlace, VoiceLines.PlayMode.Always, 1f, false, 0f, 3,
                    "And last place — hey, somebody has to be. You were honest. That was the mistake.",
                    "Last place, don't worry about it. Everyone forgets. In about four years.",
                    "Shout out to last place. You made everybody else feel amazing."),
            };

            list.AddRange(MinigameIntros());
            return list;
        }

        /// <summary>
        /// One intro per minigame, spoken over the rule card. Adding a minigame adds a line
        /// here automatically — the key is derived from the registry id.
        /// </summary>
        private static List<Entry> MinigameIntros()
        {
            var intros = new Dictionary<string, string>
            {
                { "green_light",  "Green Light. Do not press early. You're going to press early." },
                { "timing_bar",   "Timing Bar. Hit the gold. Looks easy. It is not." },
                { "stop_clock",   "Stop the Clock. Count it in your head, and don't lie to yourself." },
                { "keep_beat",    "Keep the Beat. Four beats, then silence. Stay with it." },
                { "press_luck",   "Press Your Luck. Same fuse for everybody. Who blinks first?" },
                { "stroop",       "Stroop. Read the colour, not the word. Your brain will fight you on this." },
                { "which_first",  "Which Was First. Blink and you'll miss it." },
                { "quick_math",   "Quick Math. Left or right, bigger one wins. Go fast." },
                { "arrow_rush",   "Arrow Rush. Follow the arrows, flip the gold ones. Try not to panic." },
                { "dodge",        "Dodge. Same storm for everybody. Don't get hit." },
                { "type_it",      "Type It. Spelling counts. Yes, really." },
                { "memory_boxes", "Memory Boxes. Remember them, then click them. Simple. Allegedly." },
                { "simon",        "Simon. Order matters. Every single time." },
                { "spot_change",  "Spot the Change. One box moves. Your eyes will lie to you." },
                { "mirror",       "Mirror. Flip it across the gold line. Take your time — not too much time." },
                { "odd_one_out",  "Odd One Out. One of them is a different colour. Squint if you have to." },
                { "sort_it",      "Sort It. Smallest to largest. Don't overthink it." },
                { "count_dots",   "Count the Dots. You won't have time to count. Guess well." },
                { "bullseye",     "Bullseye. Dead centre. Get close, get out." },
                { "steady_hand",  "Steady Hand. Just... don't move. That's the whole thing." },
            };

            var list = new List<Entry>();
            foreach (var kv in intros)
                list.Add(E(VoiceKeys.Minigame(kv.Key), VoiceLines.PlayMode.Chance, 0.85f, false, 0f, 1, kv.Value));
            return list;
        }

        /// <summary>Total number of clips the script needs — handy for the generator window.</summary>
        public static int ClipCount()
        {
            int n = 0;
            foreach (var e in All()) n += e.Texts != null ? e.Texts.Length : 0;
            return n;
        }

        public static int CharacterCount()
        {
            int n = 0;
            foreach (var e in All())
                if (e.Texts != null)
                    foreach (var t in e.Texts) n += t != null ? t.Length : 0;
            return n;
        }
    }
}
