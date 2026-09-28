using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// The comic-book timer: a gold starburst with the seconds left. "SECONDS" becomes
    /// "SECONDS!" at ten, and for the last five the burst turns ink with gold digits.
    /// Every new second gives it a small punch so the countdown is felt, not just read.
    /// </summary>
    [AddComponentMenu("Smartest/Timer Burst")]
    public class Burst : MonoBehaviour
    {
        [SerializeField] private Image star;
        [SerializeField] private TMP_Text value;
        [SerializeField] private TMP_Text unit;
        [SerializeField] private string unitText = "SECONDS";
        [SerializeField] private string urgentUnitText = "SECONDS!";
        [SerializeField] private float urgentAt = 10f;
        [SerializeField] private float inkAt = 5f;
        [Tooltip("Tick every second from here down. 0 = silent.")]
        [SerializeField] private float tickFrom;

        private int _shown = -1;
        private float _punch;

        /// <summary>Used by the builders.</summary>
        public void Wire(Image starImage, TMP_Text valueText, TMP_Text unitLabel, string unitWord, string urgentUnitWord,
            float tickFromSeconds)
        {
            star = starImage;
            value = valueText;
            unit = unitLabel;
            unitText = unitWord;
            urgentUnitText = urgentUnitWord;
            tickFrom = tickFromSeconds;
        }

        public void SetVisible(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
            if (!on) _shown = -1;
        }

        /// <summary>A fixed value with its unit ("128 POINTS"): no countdown, no ink switch.</summary>
        public void Show(string valueText, string unitWord)
        {
            if (value != null) value.text = valueText;
            if (unit != null) unit.text = unitWord ?? string.Empty;
            _punch = 1f;
        }

        /// <summary>Show a fixed value (the lead-in digits), with no unit and no ink switch.</summary>
        public void SetDigit(string digit)
        {
            if (value != null && value.text != digit)
            {
                value.text = digit;
                _punch = 1f;
            }
            if (unit != null) unit.text = string.Empty;
        }

        public void Set(float remaining)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            if (s == _shown) return;

            bool ink = remaining <= inkAt;
            if (star != null) star.color = ink ? Palette.Ink : Palette.Gold;
            if (value != null)
            {
                value.text = s.ToString();
                value.color = ink ? Palette.Gold : Palette.Ink;
            }
            if (unit != null)
            {
                unit.text = s <= urgentAt ? urgentUnitText : unitText;
                unit.color = ink ? Palette.Gold : Palette.Ink;
            }

            if (_shown >= 0 && s < _shown)
            {
                _punch = 1f;
                if (tickFrom > 0f && s <= tickFrom && s > 0) Sounds.Play(Sounds.Kind.Tick, 0.7f);
            }
            _shown = s;
        }

        private void OnDisable()
        {
            _punch = 0f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (_punch <= 0f) return;
            _punch = Mathf.Max(0f, _punch - Time.unscaledDeltaTime / 0.28f);
            float k = 1f + 0.14f * Tween.Evaluate(Ease.OutQuad, _punch);
            transform.localScale = new Vector3(k, k, 1f);
        }
    }
}
