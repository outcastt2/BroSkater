using UnityEngine;
using UnityEngine.InputSystem;

public class InputReader : MonoBehaviour
{
    private InputSystem_Actions _playerControls;

    // Input actions
    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _grindAction;

    // Public properties
    public Vector2 MovementInput => _moveAction.ReadValue<Vector2>();
    public bool JumpInput => _jumpAction.WasPressedThisFrame();
    public bool GrindInput => _grindAction.WasPressedThisFrame();

    private void Awake()
    {
        _playerControls = new InputSystem_Actions();
    }

    // Enable input
    public void EnableGameplayInput()
    {
        _playerControls.Enable();
        _moveAction = _playerControls.Player.Move;
        _jumpAction = _playerControls.Player.Jump;
        _grindAction = _playerControls.Player.Grind;
    }

    // Disable input
    public void DisableGameplayInput()
    {
        _playerControls.Disable();
        _moveAction = null;
        _jumpAction = null;
        _grindAction = null;
    }
} 