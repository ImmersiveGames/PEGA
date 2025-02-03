using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Drivers
{
    public class PlayerMovementDriver : IMovementDriver
    {
        private readonly CharacterInputHandler _inputHandler;

        public PlayerMovementDriver(CharacterInputHandler inputHandler)
        {
            _inputHandler = inputHandler;
        }

        public Vector2 GetMovementPressing() => _inputHandler.GetMovementDirection();
        public bool IsJumpPressing() => _inputHandler.IsActionPressed(ActionsNames.Jump);
        public bool IsDashPressing() => _inputHandler.IsActionPressed(ActionsNames.Dash);
    }
}