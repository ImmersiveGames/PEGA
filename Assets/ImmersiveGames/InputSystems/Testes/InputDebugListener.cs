using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems.Testes
{
    public class InputDebugListener : MonoBehaviour
    {
        private void Start()
        {
            var inputHandler = FindObjectOfType<CharacterInputHandler>(); // Acha o handler na cena
            if (inputHandler != null)
            {
                inputHandler.OnInputReceived += HandleInputDebug;
            }
        }

        private void HandleInputDebug(string action, InputAction.CallbackContext context)
        {
            Debug.Log($"🔥 Input recebido: {action} - {context.phase}");
        }
    }
}