using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Smartest.Core
{
    /// <summary>
    /// Tiny input shim so game code doesn't care whether the project uses the new
    /// Input System, the legacy Input Manager, or both. Typed text is not handled here —
    /// the one game that needs it uses a TMP_InputField, which works on both backends.
    /// </summary>
    public static class KeyInput
    {
        public static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        public static bool EnterPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }

        public static bool SpacePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && kb.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }

        /// <summary>1..9 on the number row or the numpad. Returns 0 when nothing was pressed.</summary>
        public static int DigitPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return 0;
            if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) return 1;
            if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) return 2;
            if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) return 3;
            if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) return 4;
            if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) return 5;
            if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) return 6;
            if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) return 7;
            if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) return 8;
            if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame) return 9;
            return 0;
#else
            for (int i = 1; i <= 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i)) return i;
            return 0;
#endif
        }

        /// <summary>Which of WASD (or the arrow keys) was pressed this frame: 0 none, 1 up, 2 right, 3 down, 4 left.</summary>
        public static int DirectionPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return 0;
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) return 1;
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) return 2;
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) return 3;
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) return 4;
            return 0;
#else
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) return 1;
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) return 2;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) return 3;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) return 4;
            return 0;
#endif
        }

        /// <summary>Held WASD / arrows as a direction, not normalised.</summary>
        public static Vector2 MoveAxis()
        {
            float x = 0f, y = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            }
#else
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
#endif
            return new Vector2(x, y);
        }

        public static Vector2 MousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null ? m.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }

        public static bool MousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m != null && m.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }
}
