using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Rollzy
{
    public enum SwipeDirection { Left, Right, Up, Down }

    /// <summary>
    /// Unified touch / mouse / keyboard input.
    /// A swipe fires as soon as the finger travels far enough — no need to lift — so lane
    /// changes feel instant. One swipe per touch. A short touch with no swipe is a tap.
    /// Works with either the new Input System (Unity 6 default) or the legacy Input Manager.
    /// </summary>
    public class SwipeInput : MonoBehaviour
    {
        [Tooltip("Swipe distance as a fraction of the screen's shorter side.")]
        [SerializeField] float swipeThreshold = 0.06f;
        [SerializeField] float maxTapDuration = 0.35f;

        public event Action<SwipeDirection> Swiped;
        public event Action Tapped;
        public event Action BackPressed;

        /// <summary>Return true for screen positions covered by UI (buttons, menus) so they don't steer.</summary>
        public Func<Vector2, bool> IsBlocked;

        bool tracking;
        bool swipedThisTouch;
        Vector2 touchStart;
        float touchStartTime;

        void Update()
        {
            ReadKeyboard();
            ReadPointer();
        }

        void ReadPointer()
        {
            bool pressed = TryGetPointer(out Vector2 position);

            if (pressed && !tracking)
            {
                tracking = true;
                // A touch that starts on UI is ignored for the whole gesture.
                swipedThisTouch = IsBlocked != null && IsBlocked(position);
                touchStartTime = swipedThisTouch ? float.NegativeInfinity : Time.unscaledTime;
                touchStart = position;
                return;
            }

            if (pressed)
            {
                if (swipedThisTouch) return;

                Vector2 delta = position - touchStart;
                float threshold = Mathf.Max(24f, Mathf.Min(Screen.width, Screen.height) * swipeThreshold);
                if (delta.sqrMagnitude >= threshold * threshold)
                {
                    swipedThisTouch = true;
                    Swiped?.Invoke(ToDirection(delta));
                }
                return;
            }

            if (tracking)
            {
                tracking = false;
                if (!swipedThisTouch && Time.unscaledTime - touchStartTime <= maxTapDuration)
                    Tapped?.Invoke();
            }
        }

        static SwipeDirection ToDirection(Vector2 d)
        {
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
                return d.x > 0f ? SwipeDirection.Right : SwipeDirection.Left;
            return d.y > 0f ? SwipeDirection.Up : SwipeDirection.Down;
        }

#if ENABLE_INPUT_SYSTEM
        static bool TryGetPointer(out Vector2 position)
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                position = touch.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            position = default;
            return false;
        }

        void ReadKeyboard()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) Swiped?.Invoke(SwipeDirection.Left);
            if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) Swiped?.Invoke(SwipeDirection.Right);
            if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                Swiped?.Invoke(SwipeDirection.Up);
            if (kb.enterKey.wasPressedThisFrame) Tapped?.Invoke();
            if (kb.escapeKey.wasPressedThisFrame) BackPressed?.Invoke(); // also the Android back button
        }
#else
        static bool TryGetPointer(out Vector2 position)
        {
            if (Input.touchCount > 0)
            {
                position = Input.GetTouch(0).position;
                return true;
            }

            if (Input.GetMouseButton(0))
            {
                position = Input.mousePosition;
                return true;
            }

            position = default;
            return false;
        }

        void ReadKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Swiped?.Invoke(SwipeDirection.Left);
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Swiped?.Invoke(SwipeDirection.Right);
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space))
                Swiped?.Invoke(SwipeDirection.Up);
            if (Input.GetKeyDown(KeyCode.Return)) Tapped?.Invoke();
            if (Input.GetKeyDown(KeyCode.Escape)) BackPressed?.Invoke(); // also the Android back button
        }
#endif
    }
}
