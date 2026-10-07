using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FortressClash.Cutscene
{
    /// <summary>
    /// Thin input shim for the cutscene: a tap (mouse or touch), the skip key, and where the
    /// pointer is.  Uses the Input System package when the project has it and falls back to the
    /// legacy Input class otherwise, so the cutscene keeps working either way.
    /// </summary>
    public static class CutsceneInput
    {
        /// <summary>True on the frame the reader taps the screen (press, not release).</summary>
        public static bool TapDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Touchscreen touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                {
                    return true;
                }

                Mouse mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    return true;
                }

                return false;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        /// <summary>Space, Enter or Escape - hurry the cutscene along / leave it.</summary>
        public static bool SkipDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard keyboard = Keyboard.current;
                if (keyboard == null)
                {
                    return false;
                }

                return keyboard.spaceKey.wasPressedThisFrame
                       || keyboard.enterKey.wasPressedThisFrame
                       || keyboard.numpadEnterKey.wasPressedThisFrame
                       || keyboard.escapeKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Space)
                       || Input.GetKeyDown(KeyCode.Return)
                       || Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }

        public static bool EscapeDown
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Keyboard keyboard = Keyboard.current;
                return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }

        public static Vector2 PointerPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Touchscreen touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.isPressed)
                {
                    return touch.primaryTouch.position.ReadValue();
                }

                Mouse mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
                return Input.mousePosition;
#endif
            }
        }
    }
}
