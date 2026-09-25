using System;
using Smartest.Core;
using Smartest.UI;
using TMPro;
using UnityEngine;

namespace Smartest.Minigames
{
    /// <summary>
    /// The one panel every minigame plays inside. It owns the title, the single-line rule,
    /// the level badge, how many players are left, the countdown and the lead-in "3 2 1",
    /// so a minigame itself only ever draws its own content in the middle.
    /// </summary>
    public class MinigameStage : Panel
    {
        [Header("Chrome")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text ruleText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text aliveText;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text controlsText;
        [SerializeField] private TMP_Text bigCountText;
        [SerializeField] private RectTransform contentArea;
        [SerializeField] private CountdownBar countdown;
        [Tooltip("The same rounded 9-slice sprite the rest of the UI uses.")]
        [SerializeField] private Sprite panelSprite;

        /// <summary>This player's result for the level just played: (failed, metric).</summary>
        public event Action<bool, int> LevelFinished;

        private MinigameView _view;
        private MinigameEntry _entry;
        private float _startIn;
        private float _deadline;
        private int _lastCount = -1;
        private bool _begun;
        private bool _armed;
        private bool _playing;

        protected override void Awake()
        {
            base.Awake();
            UiKit.RoundedSprite = panelSprite;
            if (UiKit.Font == null && titleText != null) UiKit.Font = titleText.font;
        }

        // ------------------------------------------------------------------

        /// <summary>Rule card between challenges: title and rule only, no content yet.</summary>
        public void ShowIntro(string title, string rule)
        {
            DestroyView();
            if (titleText != null) titleText.text = (title ?? string.Empty).ToUpperInvariant();
            if (ruleText != null) ruleText.text = rule ?? string.Empty;
            if (levelText != null) levelText.text = string.Empty;
            if (aliveText != null) aliveText.text = string.Empty;
            if (controlsText != null) controlsText.text = string.Empty;
            if (statusText != null) statusText.text = string.Empty;
            if (bigCountText != null) bigCountText.text = string.Empty;
            if (countdown != null) countdown.SetVisible(false);
        }

        /// <summary>
        /// Build and arm one level. <paramref name="playing"/> is false for players already
        /// knocked out — they see exactly the same level, they just can't affect it.
        /// </summary>
        public void StartLevel(MinigameEntry entry, int level, int seed, bool playing, int aliveCount,
            float startIn, float levelSeconds, bool tieBreak)
        {
            if (entry == null) return;
            _entry = entry;
            _playing = playing;
            _startIn = Mathf.Max(0f, startIn);
            _deadline = _startIn + levelSeconds;
            _lastCount = -1;
            _begun = false;
            _armed = true;

            if (titleText != null) titleText.text = entry.Title.ToUpperInvariant();
            if (ruleText != null) ruleText.text = entry.Rule;
            if (levelText != null)
                levelText.text = tieBreak ? "TIE-BREAK" : $"LEVEL {level}";
            if (aliveText != null)
                aliveText.text = aliveCount > 1 ? $"{aliveCount} LEFT" : string.Empty;
            if (controlsText != null) controlsText.text = entry.Controls ?? string.Empty;
            if (statusText != null)
                statusText.text = playing ? string.Empty : "OUT — watching";
            if (countdown != null) countdown.SetVisible(true);

            DestroyView();
            if (contentArea != null && entry.ViewType != null)
            {
                // The view lives beside the content area, not inside it, because clearing
                // the area between levels would otherwise destroy the view along with it.
                var go = new GameObject("View", typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.sizeDelta = Vector2.zero;
                _view = go.AddComponent(entry.ViewType) as MinigameView;
                if (_view != null)
                {
                    _view.Init(contentArea);
                    _view.Finished += OnViewFinished;
                    _view.Prepare(level, LevelRng.For(entry.Id, seed, level), levelSeconds, playing);
                }
                else
                {
                    Debug.LogError($"[MinigameStage] {entry.ViewType} is not a MinigameView.");
                    Destroy(go);
                }
            }
        }

        /// <summary>Between levels: who went out, or that everyone has to go again.</summary>
        public void ShowLevelResult(string text)
        {
            _armed = false;
            if (_view != null) _view.Freeze();
            if (statusText != null) statusText.text = text ?? string.Empty;
            if (bigCountText != null) bigCountText.text = string.Empty;
            if (countdown != null) countdown.SetVisible(false);
        }

        public void EndMinigame()
        {
            _armed = false;
            DestroyView();
            if (countdown != null) countdown.SetVisible(false);
            if (bigCountText != null) bigCountText.text = string.Empty;
        }

        // ------------------------------------------------------------------

        private void Update()
        {
            if (!_armed) return;

            // Lead-in: the same "3, 2, 1" on every machine, ending at the same instant.
            if (!_begun)
            {
                _startIn -= Time.unscaledDeltaTime;
                _deadline -= Time.unscaledDeltaTime;
                int n = Mathf.CeilToInt(_startIn);
                if (bigCountText != null)
                {
                    bigCountText.text = n > 0 ? n.ToString() : "GO";
                    bigCountText.color = n > 0 ? Palette.Text : Palette.Accent;
                }
                if (n != _lastCount)
                {
                    _lastCount = n;
                    if (n > 0) Sounds.Play(Sounds.Kind.Tick);
                }
                if (_startIn <= 0f)
                {
                    _begun = true;
                    Sounds.Play(Sounds.Kind.Go);
                    if (_view != null) _view.Begin();
                    if (bigCountText != null) bigCountText.text = string.Empty;
                }
                return;
            }

            _deadline -= Time.unscaledDeltaTime;
            float total = _entry != null ? _entry.LevelSeconds : 10f;
            if (countdown != null) countdown.Set(Mathf.Max(0f, _deadline), total);

            if (_deadline <= 0f)
            {
                _armed = false;
                if (_view != null && !_view.IsDone) _view.TimeOut();
            }
        }

        private void OnViewFinished(bool failed, int metric)
        {
            if (!_playing) return;
            if (statusText != null) statusText.text = failed ? "OUT" : "DONE — waiting";
            LevelFinished?.Invoke(failed, metric);
        }

        private void DestroyView()
        {
            if (_view != null)
            {
                _view.Finished -= OnViewFinished;
                _view.Teardown();
                Destroy(_view.gameObject);
                _view = null;
            }
            if (contentArea != null) UiKit.Clear(contentArea);
        }
    }
}
