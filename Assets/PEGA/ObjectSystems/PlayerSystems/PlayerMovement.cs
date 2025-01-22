using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.Strategies.Movement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.PlayerSystems
{
    public class PlayerMovement : ObjectMovement
    {
        private PlayerMaster _playerMaster;
        private PlayerInputHandler _playerInputHandler;
        
        protected override void Awake()
        {
            base.Awake();
            _playerInputHandler = GetComponent<PlayerInputHandler>();
            SetMovementStrategy(new CustomMovement());
        }

        private void Start()
        {
            InitializeInput();
        }

        protected override void SetInitialReferences()
        {
            base.SetInitialReferences();
            _playerMaster = GetComponent<PlayerMaster>();
            CharacterController = GetComponent<CharacterController>();
        }

        private void InitializeInput()
        {
            // Ativa o mapa de ação local para este jogador
            _playerInputHandler.ActivateActionMap("Player");
            
            // Registro de ações do tipo eixo
            _playerInputHandler.ActionManager.RegisterAction("Axis_Move_Performed", InputAxisPerformed);
            _playerInputHandler.ActionManager.RegisterAction("Axis_Move_Cancel", InputAxisCanceled);

            DebugManager.Log<PlayerMovement>($"Mapa de ação atual: {_playerInputHandler.ActionManager.IsLocalActionMapActive(ActionManager.GameActionMaps.Player)}");
        }

        #region Input Actions

        private void InputAxisPerformed(InputAction.CallbackContext context)
        {
            InputVector = context.ReadValue<Vector2>().normalized;
            _playerMaster.OnEventPlayerMasterAxisMovement(InputVector);
            DebugManager.Log<PlayerMovement>($"Eixo performed: {InputVector}");
        }

        private void InputAxisCanceled(InputAction.CallbackContext context)
        {
            InputVector = Vector2.zero;
            _playerMaster.OnEventPlayerMasterAxisMovement(InputVector);
            DebugManager.Log<PlayerMovement>($"Eixo Canceled: {InputVector}");
        }

        #endregion
    }
}
