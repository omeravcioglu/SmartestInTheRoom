using System;
using Smartest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>
    /// One answer choice (big red/green panel, yes/no panel, or a number tile).
    /// Hover/press come from the Button's color block; "locked" dims the others and
    /// shows a check mark on the chosen one. Once locked, nothing is clickable.
    /// </summary>
    public class AnswerButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text checkMark;
        [SerializeField] private CanvasGroup group;

        public int Value { get; private set; }
        public event Action<AnswerButton> Clicked;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(() => Clicked?.Invoke(this));
            if (checkMark != null) checkMark.gameObject.SetActive(false);
        }

        public void Setup(int value, string text, Color color)
        {
            Value = value;
            if (label != null) label.text = text;
            if (background != null) background.color = color;
            ResetState();
        }

        public void SetInteractable(bool on)
        {
            if (button != null) button.interactable = on;
        }

        public void ResetState()
        {
            if (button != null) button.interactable = true;
            if (group != null) group.alpha = 1f;
            if (checkMark != null) checkMark.gameObject.SetActive(false);
            transform.localScale = Vector3.one;
        }

        public void SetLocked(bool chosen)
        {
            if (button != null) button.interactable = false;
            if (group != null) group.alpha = chosen ? 1f : 0.32f;
            if (checkMark != null) checkMark.gameObject.SetActive(chosen);
            if (chosen && isActiveAndEnabled)
                StartCoroutine(Tween.ScaleUniform(transform, 1.03f, 0.18f, Ease.OutBack));
        }
    }
}
