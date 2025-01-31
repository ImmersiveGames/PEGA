using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.Modifications;
using PEGA.ObjectSystems.ObjectsScriptables;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Handlers
{
    public class DashHandler
    {
        private readonly CharacterInputHandler _characterInput;
        private readonly ModifierController _modifierController;
        private readonly float _actualSpeed;
        private const float MultiplyDash = 4f;
        private readonly Transform _objectMovement;
        
        private readonly VerticalMovementState _verticalMovementState;
        
        public DashHandler(CharacterInputHandler controller, MovementSettings movementSettings, AttributesBaseData attributesBaseData, VerticalMovementState verticalMovementState, ModifierController modifierController)
        {
            _characterInput = controller;
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

                actualMovement.x = _characterInput.GetMovementDirection().x * (_actualSpeed + speedModifier) * MultiplyDash;
                actualMovement.z = _characterInput.GetMovementDirection().y * (_actualSpeed + speedModifier) * MultiplyDash;

                appliedMovement.x = actualMovement.x;
                appliedMovement.z = actualMovement.z;
            }
            else if (!isDashPressed)
            {
                isDashing = false; // Permite um novo pulo após liberar o botão
                actualMovement.x = _characterInput.GetMovementDirection().x * _actualSpeed;
                actualMovement.z = _characterInput.GetMovementDirection().y * _actualSpeed;

                appliedMovement.x = actualMovement.x;
                appliedMovement.z = actualMovement.z;
            }
            
        }
    }
}
