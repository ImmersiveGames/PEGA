﻿using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.Modifications;
using PEGA.ObjectSystems.ObjectsScriptables;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Handlers
{
    public class MovementHandler
    {
        private readonly MovementController _movementController;
        private readonly ModifierController _modifierController;
        private readonly float _actualSpeed;
        private readonly float _rotationPerFrame;
        private readonly Transform _objectMovement;

        public MovementHandler(Transform mainObject, MovementController movementController, MovementSettings movementSettings, AttributesBaseData attributesBaseData, ModifierController modifierController)
        {
            _objectMovement = mainObject;
            _movementController = movementController;
            
            _modifierController = modifierController;
            _actualSpeed = movementSettings.baseSpeed + attributesBaseData.attAgility;
            _rotationPerFrame = movementSettings.baseSpeed + attributesBaseData.attAgility + attributesBaseData.attBase;
        }

        public void UpdateMovement(ref Vector3 actualMovement, ref Vector3 appliedMovement)
        {
            HandleRotate(ref actualMovement, _objectMovement, _rotationPerFrame);
            HandleMovement(ref actualMovement, ref appliedMovement);
        }

        private void HandleMovement(ref Vector3 actualMovement, ref Vector3 appliedMovement)
        {
            var speedModifier = _modifierController.GetModifierValue(ModifierKeys.SpeedMultiplay);
            speedModifier = (speedModifier == 0) ? 1f : speedModifier;

            actualMovement.x = _movementController.GetMovementPressing().x * (_actualSpeed * speedModifier);
            actualMovement.z = _movementController.GetMovementPressing().y * (_actualSpeed * speedModifier);

            appliedMovement.x = actualMovement.x;
            appliedMovement.z = actualMovement.z;
        }

        private void HandleRotate(ref Vector3 actualMovement, Transform tObject, float rotationPerFrame)
        {
            Vector3 positionToLookAt;
            positionToLookAt.x = actualMovement.x;
            positionToLookAt.y = 0.0f;
            positionToLookAt.z = actualMovement.z;

            var currentRotation = tObject.rotation;
            if (_movementController.GetMovementPressing() == Vector2.zero) return;
            var targetRotation = Quaternion.LookRotation(positionToLookAt);
            tObject.rotation = Quaternion.Slerp(currentRotation, targetRotation, rotationPerFrame * Time.deltaTime);
        }

        public static void ApplyMovement(Vector3 appliedMovement, CharacterController characterController)
        {
            characterController.Move(appliedMovement * Time.deltaTime);
        }
    }
}
