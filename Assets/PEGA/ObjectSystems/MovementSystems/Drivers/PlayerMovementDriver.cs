using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems.MovementSystems.Interfaces;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.Drivers
{
    public class PlayerMovementDriver : IMovementDriver
    {
        private readonly CharacterInputHandler _inputHandler;

        public PlayerMovementDriver(CharacterInputHandler inputHandler) => _inputHandler = inputHandler;

        public Vector2 GetMovementInput() => _inputHandler.GetMovementDirection();
        public bool IsJumping() => _inputHandler.IsActionPressed("Jump");
        public bool IsDashing() => _inputHandler.IsActionPressed("Dash");
    }
}