using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using PEGA.InputActions;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.MovementSystems.Drivers
{
    public class PlayerMovementDriver : IMovementDriver
    {
        private readonly ActionManager _actionManager;
        private readonly ActionMapManager _actionMapManager;

        public PlayerMovementDriver(PlayerInput playerInput)
        {
            _actionManager = new ActionManager(playerInput.actions);
            _actionMapManager = new ActionMapManager(playerInput);
        }

        public Vector2 GetMovementDirection()
        {
            return _actionManager.GetActionValue<Vector2>(ActionsKey.AxisMove);
        }
        public bool IsJumpingPress { get; private set; }
        public bool IsDashPress { get; private set; }

        public void InitializeDriver()
        {
            _actionMapManager.ActivateActionMap(ActionMapKey.Player);
            _actionManager.OnActionTriggered += HandleActionTriggered;
        }

        private void HandleActionTriggered(ActionsKey actionName, InputAction.CallbackContext context)
        {
            if (actionName == ActionsKey.Jump)
            {
                if (context.started) // 🔹 Pressionou o botão
                {
                    IsJumpingPress = true;
                }
                else if (context.canceled) // 🔹 Soltou o botão
                {
                    IsJumpingPress = false;
                }
            }

            if (actionName == ActionsKey.Dash)
            {
                if (context.started)
                {
                    IsDashPress = true;
                }
                else if (context.canceled)
                {
                    IsDashPress = false;
                }
            }

            DebugManager.Log<IMovementDriver>($"Action Triggered: {actionName}, Phase: {context.phase}");
        }


        public void UpdateDriver()
        {
            // Nada necessário aqui por enquanto, pois os inputs já estão sendo atualizados no evento
        }

        public void ExitDriver()
        {
            DebugManager.Log<IMovementDriver>($"Exiting player movement driver");
            _actionManager.OnActionTriggered -= HandleActionTriggered;
            _actionMapManager.RestorePreviousActionMap();
        }

        public void Reset()
        {
            DebugManager.Log<IMovementDriver>($"Reset");
            _actionMapManager.ClearHistory();
            IsJumpingPress = false;
            IsDashPress = false;
        }
    }
}
