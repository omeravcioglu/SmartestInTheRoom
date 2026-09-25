using System.Collections;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>One reveal row: name · answer chip · delta. Prefab: Prefabs/RevealRow.prefab.</summary>
    public class RevealRowView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image chipBackground;
        [SerializeField] private TMP_Text chipText;
        [SerializeField] private TMP_Text deltaText;
        [Tooltip("Small note at the end of the row — used for the winner's crown on minigames.")]
        [SerializeField] private TMP_Text noteText;

        private int _delta;

        public void Set(string playerName, string answer, Color answerColor, int delta, string note, bool isLocal)
        {
            _delta = delta;
            if (nameText != null)
            {
                nameText.text = playerName;
                nameText.color = isLocal ? Palette.Accent : Palette.Text;
            }
            if (chipBackground != null) chipBackground.color = answerColor;
            if (chipText != null) chipText.text = answer;
            if (deltaText != null)
            {
                deltaText.text = string.Empty;
                deltaText.color = Palette.Zero;
            }
            if (noteText != null)
            {
                noteText.text = note ?? string.Empty;
                noteText.gameObject.SetActive(!string.IsNullOrEmpty(note));
            }
            if (group != null) group.alpha = 0f;
        }

        /// <summary>Fade + slide in (used by the staggered reveal).</summary>
        public IEnumerator Appear(float duration)
        {
            var rt = (RectTransform)transform;
            Vector2 target = rt.anchoredPosition;
            Vector2 from = target + new Vector2(-24f, 0f);
            rt.anchoredPosition = from;
            yield return Tween.To(duration, Ease.OutCubic, t =>
            {
                if (group != null) group.alpha = t;
                rt.anchoredPosition = Vector2.LerpUnclamped(from, target, t);
            });
        }

        public void ShowDeltaImmediate()
        {
            if (deltaText == null) return;
            deltaText.text = Format(_delta);
            deltaText.color = ColorFor(_delta);
        }

        public IEnumerator CountDelta(float duration)
        {
            if (deltaText == null) yield break;
            deltaText.color = ColorFor(_delta);
            yield return Tween.CountInt(0, _delta, duration, v => deltaText.text = Format(v));
        }

        public static string Format(int d) => d > 0 ? "+" + d : d < 0 ? "−" + (-d) : "0";
        public static Color ColorFor(int d) => d > 0 ? Palette.Positive : d < 0 ? Palette.Negative : Palette.Zero;
    }
}
