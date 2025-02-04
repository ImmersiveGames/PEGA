using PEGA.InputActions;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ImmersiveGames.InputSystems.Testes
{
    public class TestInputSystem : MonoBehaviour
    {
        public InputActionAsset inputActionAsset;
        private ActionManager _actionManager;

        private void Awake()
        {
            _actionManager = new ActionManager(inputActionAsset);
            _actionManager.OnActionTriggered += (actionName, context) =>
            {
                bool isHeld = context.control.IsPressed();
                Debug.Log($"Action Triggered: {actionName}, Phase: {context.phase}, IsHeld: {isHeld}");
            };
        }
        private void Update()
        {
            if (_actionManager.IsActionActive(ActionsKey.Jump)) 
            {
                Debug.Log("Jump is being held.");
            }
        }

        private void OnEnable()
        {
            inputActionAsset.Enable();
        }

        private void OnDisable()
        {
            inputActionAsset.Disable();
        }
    }

}