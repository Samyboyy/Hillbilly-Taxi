using UnityEngine;

namespace HillbillyTaxi.Player
{
    /// <summary>
    /// A single frame of player intent. Keeping intent separate from movement lets us
    /// reuse the same input for walking, driving, menus, replays, or future prediction.
    /// </summary>
    public readonly struct CharacterInputFrame
    {
        public CharacterInputFrame(
            Vector2 move,
            Vector2 look,
            bool jumpPressed,
            bool jumpHeld,
            bool sprintHeld,
            bool interactPressed,
            bool lookComesFromMouse)
        {
            Move = move;
            Look = look;
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
            SprintHeld = sprintHeld;
            InteractPressed = interactPressed;
            LookComesFromMouse = lookComesFromMouse;
        }

        public Vector2 Move { get; }

        public Vector2 Look { get; }

        public bool JumpPressed { get; }

        public bool JumpHeld { get; }

        public bool SprintHeld { get; }

        public bool InteractPressed { get; }

        public bool LookComesFromMouse { get; }

        public CharacterInputFrame WithoutLook()
        {
            return new CharacterInputFrame(
                Move,
                Vector2.zero,
                JumpPressed,
                JumpHeld,
                SprintHeld,
                InteractPressed,
                LookComesFromMouse);
        }
    }
}
