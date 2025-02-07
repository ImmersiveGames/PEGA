using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class WalkingState : BaseState
    {
        public override StatesNames StateName => StatesNames.Walk;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;
        private readonly AnimatorHandler _animator;
        public WalkingState(MovementContext currentMovementContext, MovementStateFactory factory): base(currentMovementContext,factory)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
            _ctx = currentMovementContext;
            _factory = factory;
        }
        protected internal override void EnterState()
        {
            _animator.SetFloat("Movement", _ctx.movementDirection.magnitude);
            _ctx.isWalking = true;
            base.EnterState();
            //aqui ele aplica a lógica de animação
        }

        protected override void UpdateState()
        {
            _ctx.ApplyMovement();
           CheckSwitchState();//Manter por último
        }

        public override void ExitState()
        {
            base.ExitState();
            _ctx.isWalking = false;
        }

        public override void CheckSwitchState()
        {
            if (!_ctx.CanDashAgain && !_ctx.ActualDriver.IsDashPress && _ctx.DashCooldownTimer <= 0)
            {
                _ctx.CanDashAgain = true;
            }
            if (_ctx.CharacterController.isGrounded && _ctx.ActualDriver.IsDashPress && !_ctx.isDashing && _ctx.CanDashAgain)
            {
                Debug.Log("Dashing - Initialize - Do Walking");
                _ctx.CanDashAgain = false;
                //Aqui acho que é importante ele manda o Estado Acima, mudar.
                CurrentSuperstate.SwitchSubState(_factory.GetState(StatesNames.Dash));
                return;
            }
            if (_ctx.movementDirection == Vector2.zero)
            {
                CurrentSuperstate.SwitchSubState(_factory.GetState(StatesNames.Idle));
            }
        }
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}