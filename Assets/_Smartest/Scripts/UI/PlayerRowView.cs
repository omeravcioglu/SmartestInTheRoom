using System.Collections;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// One player row, shared by the lobby list and the in-game scoreboard.
    /// Prefab lives at Assets/_Smartest/Prefabs/PlayerRow.prefab (built by SceneBuilder).
    /// </summary>
    public class PlayerRowView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text tagText;    // "HOST", "YOU", "HOST · YOU"
        [SerializeField] private TMP_Text stateText;  // "thinking" / "locked in" (game only)
        [SerializeField] private TMP_Text deltaText;  // "+15" flash (game only)
        [SerializeField] private TMP_Text scoreText;  // game only

        public ulong ClientId { get; private set; }
        public RectTransform Rect => (RectTransform)transform;

        private Coroutine _scoreRoutine;
        private Coroutine _deltaRoutine;
        private Coroutine _moveRoutine;

        public void Bind(ulong clientId) => ClientId = clientId;

        public void SetName(string name)
        {
            if (nameText != null) nameText.text = name;
        }

        public void SetTags(bool isHost, bool isLocal)
        {
            if (tagText == null) return;
            string t = isHost && isLocal ? "HOST · YOU" : isHost ? "HOST" : isLocal ? "YOU" : string.Empty;
            tagText.text = t;
            tagText.gameObject.SetActive(!string.IsNullOrEmpty(t));
        }

        public void SetHighlight(bool isLocal)
        {
            if (background == null) return;
            var a = Palette.Accent;
            background.color = isLocal ? new Color(a.r, a.g, a.b, 0.16f) : Palette.PanelRaised;
            if (nameText != null) nameText.color = isLocal ? Palette.Accent : Palette.Text;
        }

        public void SetScore(int score, bool visible)
        {
            if (scoreText == null) return;
            scoreText.gameObject.SetActive(visible);
            scoreText.text = score.ToString();
        }

        public void SetState(string state, Color color, bool visible)
        {
            if (stateText == null) return;
            stateText.gameObject.SetActive(visible);
            stateText.text = state;
            stateText.color = color;
        }

        /// <summary>Count the score from → to and flash the delta beside it.</summary>
        public void AnimateScore(int from, int to, float duration)
        {
            if (scoreText == null) return;
            if (_scoreRoutine != null) StopCoroutine(_scoreRoutine);
            _scoreRoutine = StartCoroutine(Tween.CountInt(from, to, duration, v => scoreText.text = v.ToString()));

            int delta = to - from;
            if (deltaText != null && delta != 0)
            {
                if (_deltaRoutine != null) StopCoroutine(_deltaRoutine);
                _deltaRoutine = StartCoroutine(FlashDelta(delta, duration));
            }
        }

        private IEnumerator FlashDelta(int delta, float holdSeconds)
        {
            deltaText.gameObject.SetActive(true);
            deltaText.text = delta > 0 ? "+" + delta : "−" + (-delta);
            deltaText.color = delta > 0 ? Palette.Positive : Palette.Negative;
            deltaText.alpha = 0f;
            yield return Tween.To(0.15f, Ease.OutQuad, t => deltaText.alpha = t);
            yield return new WaitForSecondsRealtime(holdSeconds + 0.8f);
            yield return Tween.To(0.4f, Ease.InQuad, t => deltaText.alpha = 1f - t);
            deltaText.gameObject.SetActive(false);
            _deltaRoutine = null;
        }

        /// <summary>Slide to a new sorted position.</summary>
        public void MoveTo(Vector2 anchoredPosition, float duration)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            if (!isActiveAndEnabled || duration <= 0f) { Rect.anchoredPosition = anchoredPosition; return; }
            _moveRoutine = StartCoroutine(Tween.SlideAnchored(Rect, anchoredPosition, duration, Ease.OutCubic));
        }
    }
}
