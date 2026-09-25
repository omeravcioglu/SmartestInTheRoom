using UnityEngine;

namespace Smartest.Core
{
    /// <summary>How the host puts a lobby online.</summary>
    public enum NetworkMode
    {
        /// <summary>Relay if Unity Services answer, otherwise Wi-Fi. The sensible default.</summary>
        Auto = 0,
        /// <summary>Unity Relay only — friends can join from anywhere.</summary>
        Online = 1,
        /// <summary>Direct connection on the local network; the join code is this machine's IP.</summary>
        Lan = 2,
        /// <summary>127.0.0.1 only — two instances on one PC.</summary>
        Local = 3
    }

    /// <summary>
    /// Single source of truth for all tunable numbers (§7 of the build spec plus
    /// match rules and UI transition timings). One asset lives at
    /// Assets/_Smartest/Data/GameConfig.asset and is referenced by the bootstrap.
    /// </summary>
    [CreateAssetMenu(menuName = "Smartest/Game Config", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("Round timing (seconds)")]
        [Tooltip("Title + prompt fade in, buttons appear, intro voice line.")]
        public float roundIntroSeconds = 2.0f;

        [Tooltip("Default answering window. A RoundDefinition may override per round.")]
        public float defaultAnswerSeconds = 30f;

        [Tooltip("Extra time after all players lock in before the answering phase ends.")]
        public float lockAllGraceSeconds = 1.5f;

        [Tooltip("Reveal duration is (players * this), clamped to a minimum.")]
        public float revealSecondsPerRow = 1.0f;
        public float revealMinSeconds = 3.0f;

        [Tooltip("Scoring phase: deltas count up, scoreboard reorders, reaction text.")]
        public float scoringSeconds = 2.5f;

        [Header("Match rules")]
        public int targetScore = 100;
        public int maxPlayers = 8;
        [Tooltip("Alternate social rounds and minigames. Off = one shuffled pool of everything.")]
        public bool alternateSocialAndMinigame = true;

        [Header("Minigames")]
        [Tooltip("Rule card before the first level.")]
        public float minigameIntroSeconds = 2.5f;
        [Tooltip("The 3-2-1 before each level. Everyone starts on the same instant.")]
        public float minigameLeadInSeconds = 2.0f;
        [Tooltip("How long 'OUT: Ayse, Mert' stays up between levels.")]
        public float levelResultSeconds = 1.6f;
        [Tooltip("Extra time after the last player reports, before the level is closed.")]
        public float levelReportGraceSeconds = 0.4f;
        [Tooltip("Safety valve: a minigame can never run more levels than this.")]
        public int minigameMaxLevels = 20;

        [Header("Minigame payout")]
        [Tooltip("Points for 1st, 2nd, 3rd... Last place always gets the penalty below instead.")]
        public int[] minigamePlacePoints = { 20, 10, 5 };
        [Tooltip("Last place, whenever two or more players took part.")]
        public int minigameLastPlacePoints = -5;
        [Tooltip("Playing alone: points for clearing every solo level.")]
        public int minigameSoloClearPoints = 20;
        [Tooltip("Playing alone: how many levels to clear.")]
        public int minigameSoloLevels = 3;

        [Header("Networking")]
        [Tooltip("Auto: try Unity Relay, fall back to Wi-Fi hosting if the services aren't reachable.\n" +
                 "Online: Relay only.\n" +
                 "Wi-Fi: host on this machine's LAN address; friends on the same network join by IP.\n" +
                 "Local: 127.0.0.1 only, for two instances on this PC.")]
        public NetworkMode networkMode = NetworkMode.Auto;
        [Tooltip("Port used for Wi-Fi and local hosting.")]
        public ushort localPort = 7777;
        [Tooltip("Seconds to wait for a client connection before giving up.")]
        public float connectTimeoutSeconds = 12f;
        [Tooltip("Max characters in a player name.")]
        public int maxNameLength = 12;

        [Header("UI transitions (seconds)")]
        public float panelShowSeconds = 0.25f;
        public float panelHideSeconds = 0.18f;

        [Tooltip("Panels scale from this to 1.0 when shown.")]
        [Range(0.5f, 1f)] public float panelFromScale = 0.96f;

        [Tooltip("Panels slide from this offset (in reference pixels) when shown.")]
        public Vector2 panelSlideOffset = new Vector2(0f, -24f);

        [Header("Countdown")]
        [Tooltip("Bar turns the accent color at this many seconds remaining.")]
        public float countdownAccentAt = 10f;
        [Tooltip("Bar pulses and numeric countdown appears at this many seconds remaining.")]
        public float countdownPulseAt = 5f;

        [Header("Reveal animation")]
        [Tooltip("Delay between staggered reveal rows.")]
        public float revealRowStagger = 0.12f;
        [Tooltip("How long a score value counts up/down.")]
        public float scoreCountDuration = 0.6f;
    }
}
