using Smartest.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Smartest.UI
{
    /// <summary>The square beside the sound button in a match: three ink bars. Opens the match menu, as Esc does.</summary>
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("Smartest/Menu Button")]
    public class MenuButton : MonoBehaviour
    {
        [SerializeField] private MatchMenu menu;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            if (menu == null) menu = FindAnyObjectByType<MatchMenu>(FindObjectsInactive.Include);
            if (menu != null) menu.Toggle();
        }

        /// <summary>A paper square with a hard shadow, like the sound button, and three bars in it.</summary>
        public static MenuButton Create(Transform parent, MatchMenu menu, float left, float top, float size = 56f)
        {
            var box = Ink.Box(parent, "MenuButton", Palette.Paper, 3f, 0f, raycast: true);
            box.rectTransform.At(left, top, size, size);
            Ink.Shadow(box, 4f);
            var btn = box.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = box;
            box.gameObject.AddComponent<SlabPress>();
            float w = Mathf.Round(size * 0.46f);
            float bar = Mathf.Max(3f, Mathf.Round(size * 0.07f));
            float gap = (w - 3f * bar) * 0.5f;
            float x = (size - w) * 0.5f, y = (size - w) * 0.5f;
            for (int i = 0; i < 3; i++)
                Ink.Plain(box.transform, "Bar" + i, Palette.Ink).rectTransform.At(x, y + i * (bar + gap), w, bar);
            var mb = box.gameObject.AddComponent<MenuButton>();
            mb.menu = menu;
            return mb;
        }
    }
}
