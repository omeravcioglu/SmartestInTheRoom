using System;
using System.Collections;
using Smartest.Core;
using UnityEngine;

namespace Smartest.UI
{
    /// <summary>
    /// Base class for every screen/panel. Show()/Hide() fade the CanvasGroup and
    /// slide+scale slightly so nothing ever pops. Raycasts are disabled while hidden.
    /// All screen transitions in the game go through this.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public class Panel : MonoBehaviour
    {
        [Header("Transition")]
        [SerializeField] private float showDuration = 0.25f;
        [SerializeField] private float hideDuration = 0.18f;
        [SerializeField, Range(0.5f, 1f)] private float fromScale = 0.96f;
        [SerializeField] private Vector2 slideOffset = new Vector2(0f, -24f);
        [SerializeField] private bool hiddenOnAwake = true;
        [Tooltip("Fully deactivate the GameObject once the hide animation finishes.")]
        [SerializeField] private bool deactivateWhenHidden = false;

        private CanvasGroup _cg;
        private RectTransform _rt;
        private Coroutine _routine;

        private Vector3 _baseScale = Vector3.one;
        private Vector2 _basePos;
        private bool _baseCaptured;

        public bool IsShown { get; private set; }
        public float ShowDuration => showDuration;
        public float HideDuration => hideDuration;

        protected CanvasGroup CanvasGroup => _cg;
        protected RectTransform Rect => _rt;

        protected virtual void Awake()
        {
            CacheAndCaptureBase();
            if (hiddenOnAwake) HideInstant();
            else ShowInstant();
        }

        private void CacheAndCaptureBase()
        {
            if (_cg == null) _cg = GetComponent<CanvasGroup>();
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (!_baseCaptured)
            {
                _baseScale = _rt.localScale;
                if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
                _basePos = _rt.anchoredPosition;
                _baseCaptured = true;
            }
        }

        /// <summary>Re-read the resting position/scale (call after a layout group moved the panel).</summary>
        public void RecaptureBase()
        {
            _baseCaptured = false;
            CacheAndCaptureBase();
        }

        public void ApplyConfig(GameConfig config)
        {
            if (config == null) return;
            showDuration = config.panelShowSeconds;
            hideDuration = config.panelHideSeconds;
            fromScale = config.panelFromScale;
            slideOffset = config.panelSlideOffset;
        }

        // ---- Instant states ----

        public void ShowInstant()
        {
            CacheAndCaptureBase();
            StopRoutine();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            _cg.alpha = 1f;
            _rt.localScale = _baseScale;
            _rt.anchoredPosition = _basePos;
            SetInteractable(true);
            IsShown = true;
            OnShown();
        }

        public void HideInstant()
        {
            CacheAndCaptureBase();
            StopRoutine();
            _cg.alpha = 0f;
            _rt.localScale = _baseScale * fromScale;
            _rt.anchoredPosition = _basePos + slideOffset;
            SetInteractable(false);
            IsShown = false;
            if (deactivateWhenHidden && gameObject.activeSelf) gameObject.SetActive(false);
            OnHidden();
        }

        // ---- Animated states ----

        public Coroutine Show(Action onComplete = null)
        {
            CacheAndCaptureBase();
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            StopRoutine();
            _routine = StartCoroutine(ShowRoutine(onComplete));
            return _routine;
        }

        public Coroutine Hide(Action onComplete = null)
        {
            CacheAndCaptureBase();
            // If already inactive there is nothing to animate.
            if (!gameObject.activeSelf)
            {
                IsShown = false;
                onComplete?.Invoke();
                return null;
            }
            StopRoutine();
            _routine = StartCoroutine(HideRoutine(onComplete));
            return _routine;
        }

        private IEnumerator ShowRoutine(Action onComplete)
        {
            IsShown = true;
            SetInteractable(false); // block clicks until the panel has settled
            Vector3 toScale = _baseScale;
            Vector2 toPos = _basePos;
            Vector3 fromS = _baseScale * fromScale;
            Vector2 fromP = _basePos + slideOffset;

            yield return Tween.To(showDuration, Ease.OutCubic, t =>
            {
                _cg.alpha = t;
                _rt.localScale = Vector3.LerpUnclamped(fromS, toScale, Tween.Evaluate(Ease.OutBack, t));
                _rt.anchoredPosition = Vector2.LerpUnclamped(fromP, toPos, t);
            });

            _cg.alpha = 1f;
            _rt.localScale = toScale;
            _rt.anchoredPosition = toPos;
            SetInteractable(true);
            _routine = null;
            OnShown();
            onComplete?.Invoke();
        }

        private IEnumerator HideRoutine(Action onComplete)
        {
            SetInteractable(false);
            float startAlpha = _cg.alpha;
            Vector3 startScale = _rt.localScale;
            Vector2 startPos = _rt.anchoredPosition;
            Vector3 toScale = _baseScale * fromScale;
            Vector2 toPos = _basePos + slideOffset;

            yield return Tween.To(hideDuration, Ease.InQuad, t =>
            {
                _cg.alpha = Mathf.Lerp(startAlpha, 0f, t);
                _rt.localScale = Vector3.LerpUnclamped(startScale, toScale, t);
                _rt.anchoredPosition = Vector2.LerpUnclamped(startPos, toPos, t);
            });

            _cg.alpha = 0f;
            _rt.localScale = toScale;
            _rt.anchoredPosition = toPos;
            IsShown = false;
            _routine = null;
            if (deactivateWhenHidden) gameObject.SetActive(false);
            OnHidden();
            onComplete?.Invoke();
        }

        private void SetInteractable(bool on)
        {
            _cg.interactable = on;
            _cg.blocksRaycasts = on;
        }

        private void StopRoutine()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
        }

        /// <summary>Called after a show finishes (animated or instant). Override for panel-specific setup.</summary>
        protected virtual void OnShown() { }

        /// <summary>Called after a hide finishes (animated or instant).</summary>
        protected virtual void OnHidden() { }
    }
}
