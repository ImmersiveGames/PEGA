﻿using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class IdleState: BaseState
    {
        protected override StatesNames StateName => StatesNames.Idle;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;
        private readonly AnimatorHandler _animator;
        public IdleState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
            _ctx = currentMovementContext;
            _factory = factory;
        }

        protected internal override void OnEnter()
        {
            _animator.SetFloat("Movement", 0);
            _animator.SetFloat("Idle", 0);
            _ctx.isWalking = false;
            _ctx.appliedMovement.x = 0;
            _ctx.appliedMovement.z = 0;
            base.OnEnter();
        }

        protected override void Tick()
        {
            DebugManager.Log<IdleState>($"Update - Idle");
            base.Tick();
        }

        protected override void CheckSwitchState()
        {
            if (!_ctx.CanDashAgain && !_ctx.InputDriver.IsDashPress && !_ctx.DashingCooldown)
            {
                _ctx.CanDashAgain = true;
            }
            if (_ctx.CharacterController.isGrounded && _ctx.InputDriver.IsDashPress && !_ctx.isDashing && _ctx.CanDashAgain)
            {
                Debug.Log("Dashing - Initialize - Do Idle");
                _ctx.CanDashAgain = false;
                //Aqui acho que é importante ele manda o Estado Acima, mudar.
                InMySuperState.SwitchSubState(_factory.GetState(StatesNames.Dash));
                return;
            }
            
            if (_ctx.InputDriver.GetMovementDirection() != Vector2.zero)
            {
                InMySuperState.SwitchSubState(_factory.GetState(StatesNames.Walk));
            }
        } 
        
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubStatesOnEnter()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}