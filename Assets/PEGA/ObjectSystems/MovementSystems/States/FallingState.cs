﻿using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class FallingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Fall;
        public FallingState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext, factory)
        {
            IsRootState = true;
        }

        protected internal override void EnterState()
        {
            InitializeSubState();
            Ctx.isFalling = true;
            Ctx.CalculateJumpVariables();
            base.EnterState();
        }

        protected override void UpdateState()
        {
            Ctx.HandleGravityFall();
        }

        protected override void ExitState()
        {
            Ctx.isFalling = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (Ctx.CharacterController.isGrounded)
            {
                SwitchState(Factory.Grounded());
            }
            
            //Todo: Aqui precisa registrar um tempo ou distancia negativa maxima antes de acusar game over por cair em buraco sem fim.
        }

        public sealed override void InitializeSubState()
        {
            if (Ctx.movementDirection == Vector2.zero )
            {
                SetSubState(Factory.Idle());
            }
            else
            {
                SetSubState(Factory.Walk());
            }
        }
    }
}