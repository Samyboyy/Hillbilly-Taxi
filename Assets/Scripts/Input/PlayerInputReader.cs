using HillbillyTaxi.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HillbillyTaxi.Input
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInput))]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string PlayerActionMap = "Player";
        private const string MoveActionName = "Move";
        private const string LookActionName = "Look";
        private const string JumpActionName = "Jump";
        private const string SprintActionName = "Sprint";

        private PlayerInput _playerInput;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        private bool _inputEnabled;
        private bool _inputStateApplied;

        public bool InputEnabled => _inputEnabled;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void SetInputEnabled(bool enabled)
        {
            EnsureInitialized();

            if (_inputStateApplied && _inputEnabled == enabled)
            {
                return;
            }

            _inputStateApplied = true;
            _inputEnabled = enabled;

            if (enabled)
            {
                _playerInput.enabled = true;
                _playerInput.ActivateInput();

                if (_playerInput.currentActionMap == null ||
                    _playerInput.currentActionMap.name != PlayerActionMap)
                {
                    _playerInput.SwitchCurrentActionMap(PlayerActionMap);
                }

                CacheActions();
            }
            else
            {
                _playerInput.DeactivateInput();
                _playerInput.enabled = false;
            }
        }

        public CharacterInputFrame ReadFrame()
        {
            EnsureInitialized();

            if (!_inputEnabled)
            {
                return default;
            }

            Vector2 move = Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1f);
            Vector2 look = _lookAction.ReadValue<Vector2>();
            bool jumpPressed = _jumpAction.WasPressedThisFrame();
            bool sprintHeld = _sprintAction.IsPressed();
            bool lookComesFromMouse = _lookAction.activeControl?.device is Mouse;

            return new CharacterInputFrame(
                move,
                look,
                jumpPressed,
                sprintHeld,
                lookComesFromMouse);
        }

        private void EnsureInitialized()
        {
            if (_playerInput != null)
            {
                return;
            }

            _playerInput = GetComponent<PlayerInput>();

            if (_playerInput.actions == null)
            {
                throw new MissingReferenceException(
                    $"{nameof(PlayerInputReader)} on '{name}' needs an Input Actions asset assigned to PlayerInput.");
            }

            CacheActions();
        }

        private void CacheActions()
        {
            _moveAction = _playerInput.actions.FindAction(MoveActionName, true);
            _lookAction = _playerInput.actions.FindAction(LookActionName, true);
            _jumpAction = _playerInput.actions.FindAction(JumpActionName, true);
            _sprintAction = _playerInput.actions.FindAction(SprintActionName, true);
        }
    }
}
