using System;
using System.Collections;
using UnityEngine;

namespace Smartest.Core
{
    public enum Ease
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        OutCubic,
        OutBack
    }

    /// <summary>
    /// Tiny coroutine-based tween helper. No external libraries.
    /// Everything is driven by the core <see cref="To"/> coroutine which lerps a
    /// normalized 0..1 value with easing and hands it to a callback each frame.
    /// UI tweens use unscaled time so they keep running if Time.timeScale changes.
    ///
    /// Usage from any MonoBehaviour:
    ///   StartCoroutine(Tween.Fade(canvasGroup, 1f, 0.25f, Ease.OutQuad));
    ///   StartCoroutine(Tween.To(0.6f, Ease.OutCubic, t => label.text = Mathf.RoundToInt(Mathf.Lerp(a, b, t)).ToString()));
    /// </summary>
    public static class Tween
    {
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.Linear:    return t;
                case Ease.InQuad:    return t * t;
                case Ease.OutQuad:   return 1f - (1f - t) * (1f - t);
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                case Ease.OutCubic:  return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    float p = t - 1f;
                    return 1f + c3 * p * p * p + c1 * p * p;
                }
                default: return t;
            }
        }

        /// <summary>
        /// Core normalized tween. Calls <paramref name="onUpdate"/> with the eased 0..1
        /// value every frame, then once more with exactly 1.0, then <paramref name="onComplete"/>.
        /// </summary>
        public static IEnumerator To(float duration, Ease ease, Action<float> onUpdate,
            Action onComplete = null, bool unscaled = true)
        {
            if (onUpdate == null) yield break;

            if (duration <= 0f)
            {
                onUpdate(1f);
                onComplete?.Invoke();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                onUpdate(Evaluate(ease, elapsed / duration));
                elapsed += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }

            onUpdate(1f);
            onComplete?.Invoke();
        }

        public static IEnumerator Fade(CanvasGroup cg, float to, float duration,
            Ease ease = Ease.OutQuad, Action onComplete = null)
        {
            if (cg == null) yield break;
            float from = cg.alpha;
            yield return To(duration, ease, t => cg.alpha = Mathf.Lerp(from, to, t), onComplete);
        }

        public static IEnumerator Scale(Transform tr, Vector3 to, float duration,
            Ease ease = Ease.OutBack, Action onComplete = null)
        {
            if (tr == null) yield break;
            Vector3 from = tr.localScale;
            yield return To(duration, ease, t => tr.localScale = Vector3.LerpUnclamped(from, to, t), onComplete);
        }

        public static IEnumerator ScaleUniform(Transform tr, float to, float duration,
            Ease ease = Ease.OutBack, Action onComplete = null)
        {
            yield return Scale(tr, new Vector3(to, to, to), duration, ease, onComplete);
        }

        public static IEnumerator SlideAnchored(RectTransform rt, Vector2 to, float duration,
            Ease ease = Ease.OutCubic, Action onComplete = null)
        {
            if (rt == null) yield break;
            Vector2 from = rt.anchoredPosition;
            yield return To(duration, ease, t => rt.anchoredPosition = Vector2.LerpUnclamped(from, to, t), onComplete);
        }

        /// <summary>Count an integer up/down over time; useful for scores and deltas.</summary>
        public static IEnumerator CountInt(int from, int to, float duration, Action<int> onValue,
            Ease ease = Ease.OutQuad, Action onComplete = null)
        {
            if (onValue == null) yield break;
            yield return To(duration, ease,
                t => onValue(Mathf.RoundToInt(Mathf.Lerp(from, to, t))),
                onComplete);
        }
    }
}
