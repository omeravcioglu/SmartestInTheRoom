using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// The top of every in-game screen: the round number and what kind of round it is on the
    /// left, the race to 100 in the middle, the sound button on the right.
    /// </summary>
    [AddComponentMenu("Smartest/Masthead")]
    public class Masthead : MonoBehaviour
    {
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private TMP_Text kindText;
        [SerializeField] private RaceTrack race;

        public RaceTrack Race => race;

        /// <summary>
        /// Round block left, the race to 100 in the middle, sound right, and the double rule
        /// (6 px, then 2 px) that closes off the masthead like a newspaper's.
        /// </summary>
        public static Masthead Create(Transform frame, GameConfig config, SettingsPanel settings)
        {
            var root = Ink.Node(frame, "Masthead");
            root.At(0f, 0f, 1920f, 150f);
            var m = root.gameObject.AddComponent<Masthead>();

            m.roundText = Ink.Chip(root, "Round", "ROUND 1", TypeRole.Display, 46f, Palette.Ink, Palette.Paper, 0f,
                new RectOffset(14, 14, 4, 2), caps: true);
            Ink.BoxOf(m.roundText).Pin(48f, 29f, new Vector2(0f, 1f));
            m.kindText = Ink.Chip(root, "Kind", "SOCIAL ROUND", TypeRole.Label, 12f, new Color(1f, 1f, 1f, 0f), Palette.Ink, 0f,
                new RectOffset(8, 8, 2, 2), caps: true, tracking: 0.14f);
            Ink.BoxOf(m.kindText).Pin(40f, 87f, new Vector2(0f, 1f));

            m.race = RaceTrack.Create(root, 420f, 10f, config);
            SoundButton.Create(root, settings, 1872f - 56f, 37f, 56f);
            Ink.Plain(root, "Rule1", Palette.Ink).rectTransform.At(48f, 124f, 1824f, 6f);
            Ink.Plain(root, "Rule2", Palette.Ink).rectTransform.At(48f, 134f, 1824f, 2f);
            return m;
        }

        public void SetRound(int round, bool minigame, bool tieBreak)
        {
            if (roundText != null) roundText.text = $"ROUND {Mathf.Max(1, round)}";
            if (kindText == null) return;

            string kind = minigame ? "MINIGAME" : "SOCIAL ROUND";
            kindText.text = tieBreak ? "TIE-BREAK · " + kind : kind;
            // A minigame gets a gold tab so the change of pace registers at a glance.
            var tab = kindText.transform.parent != null ? kindText.transform.parent.GetComponent<Image>() : null;
            if (tab != null) tab.color = minigame ? Palette.Gold : new Color(1f, 1f, 1f, 0f);
        }
    }
}
