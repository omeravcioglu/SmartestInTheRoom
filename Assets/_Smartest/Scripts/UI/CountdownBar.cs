using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// Thin bar that drains as time runs out. Neutral by default, turns the accent
    /// color at 10 s, pulses at 5 s, and shows a numeric "10, 9, 8…" for the last 10 s.
    /// </summary>
    public class CountdownBar : MonoBehaviour
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private Image fillImage;
        [SerializeField] private TMP_Text numeric;

        private float _accentAt = 10f;
        private float _pulseAt = 5f;

        private void Awake()
        {
            var cfg = GameBootstrap.ConfigOrDefault;
            _accentAt = cfg.countdownAccentAt;
            _pulseAt = cfg.countdownPulseAt;
        }

        public void SetVisible(bool on)
        {
            gameObject.SetActive(on);
            if (!on && numeric != null) numeric.text = string.Empty;
        }

        public void Set(float remaining, float duration)
        {
            float p = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
            if (fill != null)
            {
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2(p, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
            }
            if (fillImage != null)
            {
                Color c = remaining <= _accentAt ? Palette.Accent : Palette.TextDim;
                if (remaining <= _pulseAt)
                {
                    float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 8f));
                    c = new Color(c.r, c.g, c.b, pulse);
                }
                fillImage.color = c;
            }
            if (numeric != null)
            {
                numeric.text = remaining <= 10f ? Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString() : string.Empty;
                numeric.color = remaining <= _pulseAt ? Palette.Red : Palette.Accent;
            }
        }
    }
}
