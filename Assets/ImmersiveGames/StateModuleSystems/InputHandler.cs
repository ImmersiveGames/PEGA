using System;
using UnityEngine;

namespace ImmersiveGames.StateModuleSystems
{
public class InputHandler : MonoBehaviour
{
    public static event Action<Vector2> OnMoveInput;
    public static event Action OnJumpPressed;
    public static event Action OnJumpReleased;

    void Update()
    {
        Vector2 moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );
        OnMoveInput?.Invoke(moveInput);

        if (Input.GetKeyDown(KeyCode.Space)) OnJumpPressed?.Invoke();
        if (Input.GetKeyUp(KeyCode.Space)) OnJumpReleased?.Invoke();
    }
}
}