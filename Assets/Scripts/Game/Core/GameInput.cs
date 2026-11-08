using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Yowol
{
    /// <summary>
    /// Single place for gameplay input. Keys follow the original design:
    /// A/D (arrows) move, Space jump, Left-click (or J) attack, E skill 1, Q skill 2, 1/2/3 weapon, Esc pause.
    /// </summary>
    public static class GameInput
    {
        /// <summary>True while dialogue/cutscene owns the input.</summary>
        public static bool Locked;

        /// <summary>Simulated input for automated play tests (CLI / script eval). Press() fires on the next frame.</summary>
        public static class Sim
        {
            public static float moveX;
            public static bool jumpHeld;
            static int jump = -1, attack = -1, skill1 = -1, skill2 = -1, advance = -1, weaponFrame = -1, pause = -1;
            static int weapon = -1;

            public static void Press(string what)
            {
                int f = Time.frameCount + 1;
                switch (what)
                {
                    case "jump": jump = f; break;
                    case "attack": attack = f; break;
                    case "skill1": skill1 = f; break;
                    case "skill2": skill2 = f; break;
                    case "advance": advance = f; break;
                    case "pause": pause = f; break;
                    case "1": weapon = 0; weaponFrame = f; break;
                    case "2": weapon = 1; weaponFrame = f; break;
                    case "3": weapon = 2; weaponFrame = f; break;
                }
            }

            public static void Reset() { moveX = 0; jumpHeld = false; jump = attack = skill1 = skill2 = advance = weaponFrame = pause = -1; }

            internal static bool Now(int frame) => frame == Time.frameCount;
            internal static bool Jump => Now(jump);
            internal static bool Attack => Now(attack);
            internal static bool Skill1 => Now(skill1);
            internal static bool Skill2 => Now(skill2);
            internal static bool Advance => Now(advance);
            internal static bool Pause => Now(pause);
            internal static int Weapon => Now(weaponFrame) ? weapon : -1;
        }

        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;

        static bool PointerOverUI => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        public static float MoveX
        {
            get
            {
                if (Locked) return 0f;
                if (Sim.moveX != 0f) return Mathf.Clamp(Sim.moveX, -1f, 1f);
                if (K == null) return 0f;
                float x = 0f;
                if (K.aKey.isPressed || K.leftArrowKey.isPressed) x -= 1f;
                if (K.dKey.isPressed || K.rightArrowKey.isPressed) x += 1f;
                return x;
            }
        }

        public static bool JumpPressed => !Locked && (Sim.Jump || (K != null &&
            (K.spaceKey.wasPressedThisFrame || K.wKey.wasPressedThisFrame || K.upArrowKey.wasPressedThisFrame)));

        public static bool JumpHeld => Sim.jumpHeld || (K != null && (K.spaceKey.isPressed || K.wKey.isPressed || K.upArrowKey.isPressed));

        public static bool AttackPressed => !Locked && (Sim.Attack ||
            (M != null && M.leftButton.wasPressedThisFrame && !PointerOverUI) || (K != null && K.jKey.wasPressedThisFrame));

        public static bool Skill1Pressed => !Locked && (Sim.Skill1 || (K != null && (K.eKey.wasPressedThisFrame || K.kKey.wasPressedThisFrame)));
        public static bool Skill2Pressed => !Locked && (Sim.Skill2 || (K != null && (K.qKey.wasPressedThisFrame || K.lKey.wasPressedThisFrame)));

        public static int WeaponSelectPressed
        {
            get
            {
                if (Locked) return -1;
                if (Sim.Weapon >= 0) return Sim.Weapon;
                if (K == null) return -1;
                if (K.digit1Key.wasPressedThisFrame || K.numpad1Key.wasPressedThisFrame) return 0;
                if (K.digit2Key.wasPressedThisFrame || K.numpad2Key.wasPressedThisFrame) return 1;
                if (K.digit3Key.wasPressedThisFrame || K.numpad3Key.wasPressedThisFrame) return 2;
                return -1;
            }
        }

        /// <summary>Advance dialogue / cutscene.</summary>
        public static bool AdvancePressed => Sim.Advance || (K != null && (K.spaceKey.wasPressedThisFrame || K.fKey.wasPressedThisFrame ||
                                               K.enterKey.wasPressedThisFrame)) ||
                                              (M != null && M.leftButton.wasPressedThisFrame);

        public static bool SkipPressed => K != null && (K.escapeKey.wasPressedThisFrame || K.sKey.wasPressedThisFrame);
        public static bool PausePressed => Sim.Pause || (K != null && K.escapeKey.wasPressedThisFrame);
    }
}
