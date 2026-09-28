using Smartest.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>The little speaker in the corner of every screen. Opens (or closes) Sound settings.</summary>
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Smartest/Sound Button")]
    public class SoundButton : MonoBehaviour
    {
        [SerializeField] private SettingsPanel panel;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        /// <summary>A paper square with a speaker, a hard shadow, and the settings behind it.</summary>
        public static SoundButton Create(Transform parent, SettingsPanel settings, float left, float top, float size = 56f)
        {
            var box = Ink.Box(parent, "SoundButton", Palette.Paper, 3f, 0f, raycast: true);
            box.rectTransform.At(left, top, size, size);
            Ink.Shadow(box, 4f);
            var btn = box.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = box;
            box.gameObject.AddComponent<SlabPress>();
            var icon = Ink.Icon(box.transform, "Speaker", InkSprites.Speaker, Palette.Ink);
            float s = size * 0.46f;
            icon.rectTransform.At((size - s) * 0.5f, (size - s) * 0.5f, s, s);
            var sb = box.gameObject.AddComponent<SoundButton>();
            sb.panel = settings;
            return sb;
        }

        private void OnClick()
        {
            if (panel == null) panel = FindAnyObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            if (panel != null) panel.Toggle();
        }
    }
}
