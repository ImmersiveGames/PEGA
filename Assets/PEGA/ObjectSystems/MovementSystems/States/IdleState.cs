using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class IdleState: BaseState
    {
        public override StatesNames StateName => StatesNames.Idle;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;
        private readonly AnimatorHandler _animator;
        public IdleState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext,factory)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
            _ctx = currentMovementContext;
            _factory = factory;
        }

        protected internal override void EnterState()
        {
            _animator.SetFloat("Movement", 0);
            _animator.SetFloat("Idle", 0);
            _ctx.isWalking = false;
            _ctx.appliedMovement.x = 0;
            _ctx.appliedMovement.z = 0;
            base.EnterState();
        }

        protected override void UpdateState()
        {
            DebugManager.Log<IdleState>($"Update - Idle");
            CheckSwitchState(); //Subs Precisam atualizar aqui. E sempre no fim.
        }

        public override void CheckSwitchState()
        {
            if (!_ctx.CanDashAgain && !_ctx.ActualDriver.IsDashPress && _ctx.DashCooldownTimer <= 0)
            {
                _ctx.CanDashAgain = true;
            }
            if (_ctx.CharacterController.isGrounded && _ctx.ActualDriver.IsDashPress && !_ctx.isDashing && _ctx.CanDashAgain)
            {
                Debug.Log("Dashing - Initialize - Do Idle");
                _ctx.CanDashAgain = false;
                //Aqui acho que é importante ele manda o Estado Acima, mudar.
                CurrentSuperstate.SwitchSubState(_factory.GetState(StatesNames.Dash));
                return;
            }
            
            if (_ctx.movementDirection != Vector2.zero)
            {
                CurrentSuperstate.SwitchSubState(_factory.GetState(StatesNames.Walk));
            }
        } 
        
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}