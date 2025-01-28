using ImmersiveGames.Utils;
using PEGA.ObjectSystems.Interfaces;
using PEGA.ObjectSystems.Modifications;
using PEGA.ObjectSystems.ObjectsScriptables;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Handlers
{
    public class DashHandler
    {
        private readonly IMovementController _controller;
        private readonly ModifierController _modifierController;
        private readonly float _actualSpeed;
        private readonly float _multiplyDash = 4f;
        private readonly Transform _objectMovement;
        
        private readonly VerticalMovementState _verticalMovementState;
        
        public DashHandler(IMovementController controller, MovementSettings movementSettings, AttributesBaseData attributesBaseData, VerticalMovementState verticalMovementState, ModifierController modifierController)
        {
            _controller = controller;
            _modifierController = modifierController;
            _verticalMovementState = verticalMovementState;
            _actualSpeed = movementSettings.baseSpeed + attributesBaseData.attAgility;
        }
        
        private void HandleDash(ref Vector3 actualMovement, ref Vector3 appliedMovement, bool isDashPressed, ref bool isDashing)
        {
            if (_verticalMovementState.CurrentState == VerticalMovementStateType.Grounded && isDashPressed && !isDashing)
            {
                isDashing = true;
                var speedModifier = _modifierController.GetModifierValue(ModifierKeys.DashSpeedBoost);

                actualMovement.x = _controller.InputVector.x * (_actualSpeed + speedModifier) * _multiplyDash;
                actualMovement.z = _controller.InputVector.y * (_actualSpeed + speedModifier) * _multiplyDash;

                appliedMovement.x = actualMovement.x;
                appliedMovement.z = actualMovement.z;
            }
            else if (!isDashPressed)
            {
                isDashing = false; // Permite um novo pulo após liberar o botão
                actualMovement.x = _controller.InputVector.x * _actualSpeed;
                actualMovement.z = _controller.InputVector.y * _actualSpeed;

                appliedMovement.x = actualMovement.x;
                appliedMovement.z = actualMovement.z;
            }
            
        }
        
        /*private float _dashSpeed;
        private float _dashDuration;
        private float _dashCooldown;
        private float _dashTimeRemaining;
        private bool _isDashing;
        private bool _isCooldownActive;

        private readonly MovementSettings _settings;
        private readonly ModifierController _modifierController;

        public bool IsDashing => _isDashing;

        public DashHandler(MovementSettings settings, ModifierController modifierController)
        {
            _settings = settings;
            _modifierController = modifierController;

            InitializeDashParameters();
        }

        private void InitializeDashParameters()
        {
            _dashSpeed = _settings.dashSpeed;
            _dashDuration = _settings.dashDuration;
            _dashCooldown = _settings.dashCooldown;
            _dashTimeRemaining = 0f;
            _isDashing = false;
            _isCooldownActive = false;
        }

        public void ActivateDash(bool isGrounded, bool isDashPressed)
        {
            if (!isGrounded || !isDashPressed || _isDashing || _isCooldownActive) return;

            // Condições validadas, ativar Dash
            _isDashing = true;
            _dashTimeRemaining = _dashDuration;
        }

        public void UpdateDash(ref Vector3 movement, IMovementController controller, ref float initialJumpVelocity)
        {
            if (_isDashing)
            {
                // Aplica modificadores ao Dash
                var speedBoost = _modifierController.GetModifierValue(ModifierKeys.DashSpeedBoost);
                var durationBoost = _modifierController.GetModifierValue(ModifierKeys.DashDurationBoost);

                float effectiveDashSpeed = _dashSpeed + speedBoost;
                movement.x = controller.InputVector.x * effectiveDashSpeed;
                movement.z = controller.InputVector.y * effectiveDashSpeed;

                _dashTimeRemaining -= Time.deltaTime;

                // Calcula o impulso adicional do salto com base no momento do dash
                initialJumpVelocity = CalculateDashJumpBoost(effectiveDashSpeed);

                if (_dashTimeRemaining <= 0)
                {
                    _isDashing = false;
                    _isCooldownActive = true;
                }
            }
            else if (_isCooldownActive)
            {
                _dashCooldown -= Time.deltaTime;
                if (_dashCooldown <= 0)
                {
                    _isCooldownActive = false;
                }
            }
        }*/
    }
}
