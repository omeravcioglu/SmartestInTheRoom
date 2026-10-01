using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// Reaction the other way round: hold the button down and let go the instant the circle
    /// turns red. It says NOW as it turns, so the colour isn't the only cue. Let go early and
    /// you're out; from level three it flashes blue first to bait you. Ranked by total
    /// reaction time.
    /// </summary>
    public class LetGoGame : MinigameView
    {
        private const float PressBy = 3f;
        private const float ReleaseBy = 1f;
        private const float Pause = 0.7f;
        private const float BaitFor = 0.25f;

        private float[] _waits = new float[0];
        private float[] _baits = new float[0]; // seconds after holding when a bait flash comes; negative for none
        private int _round;
        private float _roundStart;
        private float _heldAt = -1f;
        private float _redAt = -1f;
        private float _doneAt = -1f;
        private float _reactionSum;
        private Image _light;
        private TMP_Text _word;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int rounds;
            float minWait, maxWait;
            int baitRounds;
            // Three rounds at most: longer waits and more rounds would run past a level's length.
            switch (Level)
            {
                case 1: rounds = 2; minWait = 1.2f; maxWait = 2.8f; baitRounds = 0; break;
                case 2: rounds = 3; minWait = 1.1f; maxWait = 3.0f; baitRounds = 0; break;
                case 3: rounds = 3; minWait = 1.0f; maxWait = 3.2f; baitRounds = 1; break;
                case 4: rounds = 3; minWait = 1.0f; maxWait = 3.2f; baitRounds = 2; break;
                default: rounds = 3; minWait = 0.9f; maxWait = 3.4f; baitRounds = 3; break;
            }
            // Which rounds carry a bait flash, then when it comes: always inside its own wait.
            var baited = new bool[rounds];
            for (int k = 0; k < baitRounds; k++)
            {
                int r;
                int tries = 0;
                do r = RandomRange(0, rounds); while (baited[r] && ++tries < 10);
                baited[r] = true;
            }
            _waits = new float[rounds];
            _baits = new float[rounds];
            for (int i = 0; i < rounds; i++)
            {
                _waits[i] = RandomRange(minWait, maxWait);
                _baits[i] = baited[i] ? RandomRange(0.35f, _waits[i] - 0.4f) : -1f;
            }

            _light = UiKit.Dot(Area, "Light", 240f, new Vector2(0f, 30f), Palette.Neutral);
            _word = UiKit.Label(_light.transform, "Word", "HOLD", 60f, Palette.Ink, new Vector2(220f, 110f), Vector2.zero);
            _label = UiKit.Label(Area, "Hint", "PRESS AND HOLD", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Progress("ROUNDS", 0, rounds);
        }

        private void Show(Color fill, string word, Color ink)
        {
            _light.color = fill;
            _word.text = word;
            _word.color = ink;
        }

        protected override void OnTick(float dt)
        {
            if (_round >= _waits.Length) return;

            // Between rounds.
            if (_doneAt >= 0f)
            {
                if (Elapsed - _doneAt < Pause) return;
                _doneAt = -1f;
                _heldAt = -1f;
                _redAt = -1f;
                _roundStart = Elapsed;
                Show(Palette.Neutral, "HOLD", Palette.Ink);
                _label.text = "PRESS AND HOLD";
                _label.color = Palette.TextDim;
            }

            bool held = CanAct ? KeyInput.MouseHeld() : !Interactive;

            if (_heldAt < 0f)
            {
                if (held) { _heldAt = Elapsed; Show(Palette.Accent, "WAIT", Palette.Ink); _label.text = "LET GO ON RED"; }
                else if (CanAct && Elapsed - _roundStart > PressBy) Fail("NEVER PRESSED");
                return;
            }

            float t = Elapsed - _heldAt;
            float bait = _baits[_round];
            if (_redAt < 0f)
            {
                bool baiting = bait >= 0f && t >= bait && t < bait + BaitFor;
                if (baiting) Show(Palette.Blue, "WAIT", Palette.OnBlue);
                else Show(Palette.Accent, "WAIT", Palette.Ink);
                if (t >= _waits[_round]) { _redAt = Elapsed; Show(Palette.Red, "NOW!", Palette.OnRed); }
                else if (!held) { Fail("TOO EARLY"); return; }
                else return;
            }

            // Red: let go.
            bool released = CanAct ? !held : (!Interactive && Elapsed - _redAt >= 0.3f);
            if (released)
            {
                _reactionSum += Elapsed - _redAt;
                _label.text = Mathf.RoundToInt((Elapsed - _redAt) * 1000f) + " MS";
                _label.color = Palette.Green;
                _round++;
                Progress("ROUNDS", _round, _waits.Length);
                _doneAt = Elapsed;
                Show(Palette.Neutral, "", Palette.Ink);
                if (_round >= _waits.Length && CanAct) Finish(false, Ms(_reactionSum));
            }
            else if (CanAct && Elapsed - _redAt > ReleaseBy) Fail("TOO SLOW");
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
