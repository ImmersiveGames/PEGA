using System;
using System.Collections.Generic;
using ImmersiveGames.HierarchicalStateMachine;
using PEGA.ObjectSystems.MovementSystems.States;

namespace PEGA.ObjectSystems.MovementSystems
{
    public class MovementStateFactory : HsmFactory
    {
        public MovementStateFactory(MovementContext currentMovementContext) : base(currentMovementContext)
        {
            // 🔹 Registramos os estados com construtores
            RegisterState(StatesNames.Idle, () => new IdleState(currentMovementContext, this));
            RegisterState(StatesNames.Grounded, () => new GroundedState(currentMovementContext, this));
            RegisterState(StatesNames.Jump, () => new JumpingState(currentMovementContext, this));
            RegisterState(StatesNames.Walk, () => new WalkingState(currentMovementContext, this));
            RegisterState(StatesNames.Fall, () => new FallingState(currentMovementContext, this));
            RegisterState(StatesNames.Dash, () => new DashState(currentMovementContext, this));
            RegisterState(StatesNames.Dawn, () => new JumpingDownState(currentMovementContext, this));
        }
        
    }
}
