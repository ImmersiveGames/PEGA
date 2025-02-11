using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class WalkingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Walk;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;
        private readonly AnimatorHandler _animator;
        public WalkingState(MovementContext currentMovementContext, MovementStateFactory factory): base(currentMovementContext)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
            _ctx = currentMovementContext;
            _factory = factory;
        }
        protected internal override void OnEnter()
        {
            _animator.SetFloat("Movement", _ctx.InputDriver.GetMovementDirection().magnitude);
            _ctx.isWalking = true;
            base.OnEnter();
            //aqui ele aplica a lógica de animação
        }

        protected override void Tick()
        {
            _ctx.ApplyMovement(_ctx.InputDriver.GetMovementDirection());
            base.Tick();//Manter por último
        }

        protected override void OnExit()
        {
            base.OnExit();
            _ctx.isWalking = false;
        }

        protected override void CheckSwitchState()
        {
            if (!_ctx.CanDashAgain && !_ctx.InputDriver.IsDashPress && !_ctx.DashingCooldown)
            {
                _ctx.CanDashAgain = true;
            }
            if (_ctx.CharacterController.isGrounded && _ctx.InputDriver.IsDashPress && !_ctx.isDashing && _ctx.CanDashAgain)
            {
                Debug.Log("Dashing - Initialize - Do Walking");
                _ctx.CanDashAgain = false;
                //Aqui acho que é importante ele manda o Estado Acima, mudar.
                InMySuperState.SwitchSubState(_factory.GetState(StatesNames.Dash));
                return;
            }
            if (_ctx.InputDriver.GetMovementDirection() == Vector2.zero)
            {
                InMySuperState.SwitchSubState(_factory.GetState(StatesNames.Idle));
            }
        }
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubStatesOnEnter()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}