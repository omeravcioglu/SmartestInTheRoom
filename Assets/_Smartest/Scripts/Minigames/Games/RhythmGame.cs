using System.Collections.Generic;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.Minigames
{
    /// <summary>
    /// A rhythm game in miniature: notes fall down three lanes and you hit A, S or D (or the
    /// arrows, or click the lane) as each one crosses the line. The tune is written from the
    /// seed, so everyone plays the same one: straight beats first, then off-beats, then chords
    /// where two lanes hit together. Miss a note, or hit where there isn't one, and you're
    /// out. Ranked by how close to the beat you were, in total.
    /// </summary>
    public class RhythmGame : MinigameView
    {
        private const int Lanes = 3;
        private const float LaneW = 128f;
        private const float LaneGap = 18f;
        private const float HitY = -140f;
        private const float TopY = 240f;
        private static readonly string[] Keys = { "A", "S", "D" };

        private struct Note
        {
            public float Time;
            public int Lane;
            public bool Done;
            public RectTransform View;
        }

        private readonly List<Note> _notes = new List<Note>();
        private RectTransform _noteLayer;
        private Image[] _pads = new Image[0];
        private readonly float[] _flashUntil = new float[Lanes];
        private readonly Color[] _flash = new Color[Lanes];
        private float _lead, _window;
        private float _errorSum;
        private int _hit;
        private TMP_Text _label;

        protected override void Build()
        {
            var size = AreaSize;
            int bpm, count;
            float eighths, chords;
            switch (Level)
            {
                case 1: bpm = 96; count = 6; eighths = 0f; chords = 0f; _window = 0.16f; _lead = 1.7f; break;
                case 2: bpm = 104; count = 8; eighths = 0f; chords = 0f; _window = 0.15f; _lead = 1.6f; break;
                case 3: bpm = 112; count = 10; eighths = 0.25f; chords = 0f; _window = 0.13f; _lead = 1.45f; break;
                case 4: bpm = 120; count = 12; eighths = 0.3f; chords = 0.15f; _window = 0.12f; _lead = 1.3f; break;
                default:
                    bpm = Mathf.Min(150, 126 + (Level - 5) * 4);
                    count = Mathf.Min(18, 14 + (Level - 5));
                    eighths = 0.35f; chords = 0.2f;
                    _window = Mathf.Max(0.09f, 0.11f - (Level - 5) * 0.005f);
                    _lead = Mathf.Max(1.0f, 1.2f - (Level - 5) * 0.03f);
                    break;
            }

            // The board: three lanes, the line, and a pad per lane with its key.
            float span = Lanes * LaneW + (Lanes - 1) * LaneGap;
            UiKit.Box(Area, "Board", new Vector2(span + 28f, TopY - HitY + 70f), new Vector2(0f, (TopY + HitY - 40f) * 0.5f), Palette.Panel);
            for (int l = 0; l < Lanes; l++)
                UiKit.Fill(Area, "Lane" + l, new Vector2(LaneW, TopY - HitY), new Vector2(LaneX(l), (TopY + HitY) * 0.5f), Palette.PaperHi);
            UiKit.Fill(Area, "Line", new Vector2(span + 12f, 6f), new Vector2(0f, HitY), Palette.Ink);
            _pads = new Image[Lanes];
            for (int l = 0; l < Lanes; l++)
            {
                _pads[l] = UiKit.Cell(Area, "Pad" + l, new Vector2(LaneW - 8f, 46f), new Vector2(LaneX(l), HitY - 2f), Palette.Neutral, null, out _, Keys[l], 26f);
                _flash[l] = Palette.Neutral;
            }
            // Notes live under a mask over the lanes, so they slide in from the top edge. The
            // layer inside is shifted back so notes keep the area's coordinates.
            var clipCentre = new Vector2(0f, (TopY + HitY - 70f) * 0.5f);
            var clip = UiKit.Node(Area, "NoteClip", new Vector2(span + 12f, TopY - HitY + 70f), clipCentre);
            clip.gameObject.AddComponent<RectMask2D>();
            _noteLayer = UiKit.Node(clip, "Notes", Vector2.zero, -clipCentre);

            // The tune: on the beat at first, off-beats and chords as it gets harder, and never
            // the same lane three times running.
            float beat = 60f / bpm;
            float t = _lead + 0.6f;
            int last = -1, before = -1;
            while (_notes.Count < count)
            {
                int lane;
                do lane = RandomRange(0, Lanes); while (lane == last && lane == before);
                AddNote(t, lane);
                if (_notes.Count < count && RandomRange(0f, 1f) < chords)
                    AddNote(t, (lane + 1 + RandomRange(0, Lanes - 1)) % Lanes);
                before = last;
                last = lane;
                t += RandomRange(0f, 1f) < eighths ? beat * 0.5f : beat;
            }
            Place();

            _label = UiKit.Label(Area, "Hint", "A  S  D  ON THE LINE", 26f, Palette.TextDim,
                new Vector2(size.x - 60f, 40f), new Vector2(0f, -(size.y * 0.5f - 30f)));
            Progress("NOTES", 0, _notes.Count);
        }

        private static float LaneX(int lane) => (lane - (Lanes - 1) * 0.5f) * (LaneW + LaneGap);

        private void AddNote(float time, int lane)
        {
            var view = (RectTransform)UiKit.Box(_noteLayer, "Note" + _notes.Count, new Vector2(LaneW - 20f, 30f), new Vector2(LaneX(lane), TopY), Palette.Accent).transform;
            _notes.Add(new Note { Time = time, Lane = lane, View = view });
        }

        /// <summary>Notes slide down so they cross the line exactly on their beat.</summary>
        private void Place()
        {
            float speed = (TopY - HitY) / _lead;
            for (int i = 0; i < _notes.Count; i++)
            {
                var n = _notes[i];
                if (n.Done) continue;
                float y = HitY + (n.Time - Elapsed) * speed;
                n.View.anchoredPosition = new Vector2(LaneX(n.Lane), y);
                n.View.gameObject.SetActive(y < TopY + 20f && y > HitY - 60f);
            }
        }

        protected override void OnTick(float dt)
        {
            Place();
            for (int l = 0; l < Lanes; l++) _pads[l].color = Elapsed < _flashUntil[l] ? _flash[l] : Palette.Neutral;

            if (!Interactive)
            {
                // Someone watching sees every note hit dead on; the rule card's demo shows each key too.
                for (int i = 0; i < _notes.Count; i++)
                {
                    if (_notes[i].Done || Elapsed < _notes[i].Time) continue;
                    if (Demo) PressKey(Keys[_notes[i].Lane]);
                    Judge(i, _notes[i].Lane);
                }
                return;
            }
            if (!CanAct) return;

            int pressed = KeyInput.LanesPressed();
            if (KeyInput.MousePressed() && UiKit.LocalPoint(Area, KeyInput.MousePosition(), out var m))
                for (int l = 0; l < Lanes; l++)
                    if (Mathf.Abs(m.x - LaneX(l)) <= (LaneW + LaneGap) * 0.5f && m.y < TopY && m.y > HitY - 80f) pressed |= 1 << l;

            for (int l = 0; l < Lanes && CanAct; l++)
            {
                if ((pressed & (1 << l)) == 0) continue;
                // The nearest note still waiting in this lane.
                int best = -1;
                float bestErr = float.MaxValue;
                for (int i = 0; i < _notes.Count; i++)
                {
                    if (_notes[i].Done || _notes[i].Lane != l) continue;
                    float err = Mathf.Abs(Elapsed - _notes[i].Time);
                    if (err < bestErr) { best = i; bestErr = err; }
                }
                if (best < 0 || bestErr > _window)
                {
                    Flash(l, Palette.Red);
                    Fail(best >= 0 && _notes[best].Time > Elapsed && bestErr < 0.4f ? "TOO EARLY" : "NOTHING THERE");
                    return;
                }
                _errorSum += bestErr;
                Judge(best, l);
            }
            if (!CanAct) return;

            for (int i = 0; i < _notes.Count; i++)
            {
                if (_notes[i].Done || Elapsed <= _notes[i].Time + _window) continue;
                _notes[i].View.GetComponent<Image>().color = Palette.Red;
                Flash(_notes[i].Lane, Palette.Red);
                Fail("MISSED ONE");
                return;
            }
        }

        private void Judge(int index, int lane)
        {
            var n = _notes[index];
            n.Done = true;
            _notes[index] = n;
            n.View.gameObject.SetActive(false);
            Flash(lane, Palette.Green);
            _hit++;
            Progress("NOTES", _hit, _notes.Count);
            if (Interactive) Sounds.Play(Sounds.Kind.Tick);
            if (_hit < _notes.Count || !CanMove) return;
            _label.text = "IN THE GROOVE";
            _label.color = Palette.Green;
            Finish(false, Ms(_errorSum));
        }

        private void Flash(int lane, Color colour)
        {
            _flash[lane] = colour;
            _flashUntil[lane] = Elapsed + (Palette.Same(colour, Palette.Red) ? 99f : 0.14f);
            _pads[lane].color = colour;
        }

        private void Fail(string why)
        {
            if (_label != null) { _label.text = why; _label.color = Palette.Red; }
            Finish(true, WorstMetric);
        }
    }
}
