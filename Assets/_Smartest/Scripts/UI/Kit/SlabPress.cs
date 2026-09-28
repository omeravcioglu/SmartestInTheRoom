using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// Hover lifts a slab up-left by 3 px while its shadow grows by the same amount, so the
    /// shadow's far edge stays put and the slab seems to rise off the page. Pressing pushes it
    /// back down into its shadow. Nothing happens while the button can't be clicked.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Smartest/Slab Press")]
    public class SlabPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private float lift = 3f;
        [SerializeField] private float press = 4f;
        [Tooltip("Fade to 45% while the button can't be clicked. Off for answers, which fade themselves.")]
        [SerializeField] private bool dimWhenDisabled = true;

        private Button _button;
        private HardShadow _shadow;
        private RectTransform _rt;
        private Vector2 _applied;
        private float _baseShadow;
        private bool _hover;
        private bool _down;
        private CanvasGroup _group;
        private int _lastClickable = -1;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _shadow = GetComponent<HardShadow>();
            _rt = (RectTransform)transform;
        }

        private void OnDisable()
        {
            _hover = _down = false;
            Apply(0f);
        }

        private void Update()
        {
            bool clickable = Clickable;
            if ((_hover || _down) && !clickable) { _hover = _down = false; Apply(0f); }

            // Only the button's own switch: a panel blocks input while it fades in, and every
            // slab popping from 45% to full at the end of that fade would look broken.
            bool enabledHere = _button != null && _button.interactable;
            int state = enabledHere ? 1 : 0;
            if (!dimWhenDisabled || state == _lastClickable) return;
            _lastClickable = state;
            if (_group == null && !TryGetComponent(out _group)) _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = enabledHere ? 1f : 0.45f;
        }

        /// <summary>Answers manage their own fading; they switch this off.</summary>
        public bool DimWhenDisabled
        {
            get => dimWhenDisabled;
            set => dimWhenDisabled = value;
        }

        private bool Clickable => _button != null && _button.IsInteractable();

        public void OnPointerEnter(PointerEventData e)
        {
            _hover = true;
            if (Clickable) Apply(_down ? -press : lift);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            Apply(0f);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !Clickable) return;
            _down = true;
            Apply(-press);
        }

        public void OnPointerUp(PointerEventData e)
        {
            _down = false;
            Apply(_hover && Clickable ? lift : 0f);
        }

        /// <summary>Positive lifts toward the viewer, negative sinks into the shadow.</summary>
        private void Apply(float amount)
        {
            if (_rt == null) return;
            var target = new Vector2(-amount, amount);
            if (target == _applied) return;

            if (_shadow != null)
            {
                if (_applied == Vector2.zero) _baseShadow = _shadow.Distance;
                _shadow.Distance = Mathf.Max(0f, _baseShadow + amount);
            }
            _rt.anchoredPosition += target - _applied;
            _applied = target;
        }
    }
}
