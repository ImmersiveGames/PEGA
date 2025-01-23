using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.Interfaces;
using PEGA.ObjectSystems.ObjectsScriptables;
using PEGA.ObjectSystems.Strategies.Movement;
using PEGA.ObjectSystems.Tags;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEGA.ObjectSystems.PlayerSystems
{
    public class PlayerMovement : ObjectMovement, ISkinConfigurable
    {
        private Animator _animator; // Componente de animação do jogador
        private string _movementParameterName; // Nome do parâmetro do Blend Tree de movimento
        private PlayerMaster _playerMaster;
        private PlayerInputHandler _playerInputHandler;

        #region Unity Methods

        protected override void Awake()
        {
            base.Awake();
            _playerInputHandler = GetComponent<PlayerInputHandler>();
            SetMovementStrategy(new CharacterControllerMovement());
        }

        private void Start()
        {
            InitializeInput();
            var skinAttach = GetComponentInChildren<SkinAttach>();
            ApplySettings(skinAttach.skinSettings);
            _animator = skinAttach.GetComponentInChildren<Animator>();
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            UpdateAnimation();
        }

        #endregion
        

        protected override void SetInitialReferences()
        {
            base.SetInitialReferences();
            _playerMaster = GetComponent<PlayerMaster>();
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
        private void UpdateAnimation()
        {
            var moveMagnitude = InputVector.magnitude;
            if(_animator)
                _animator.SetFloat(_movementParameterName, moveMagnitude); // Define o parâmetro do Blend Tree de movimento
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

        public void ApplySettings(SkinSettings settings)
        {
            if (settings != null)
            {
                _movementParameterName = settings.movementParameterName;
                DebugManager.Log<PlayerMovement>($"{_movementParameterName} Asset de Configuração foi encontrado na Skin");
                return;
            }
            DebugManager.LogWarning<PlayerMovement>($"Nenhum Asset de Configuração foi encontrado na Skin");
        }
    }
}
