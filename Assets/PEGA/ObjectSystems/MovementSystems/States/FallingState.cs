using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class FallingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Fall;

        public FallingState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext,
            factory)
        {
            IsRootState = true;
        }

        protected internal override void EnterState()
        {
            Ctx.isFalling = true;
            Ctx.CalculateJumpVariables();
            base.EnterState();
        }

        protected override void UpdateState()
        {
            Ctx.ApplyGravity(falling: true);
        }

        protected override void ExitState()
        {
            Ctx.isFalling = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (Ctx.transform.position.y < Ctx.fallMaxHeight)
            {
                SwitchState(Factory.Dead());
                return;
            }

            if (Ctx.CharacterController.isGrounded)
            {
                SwitchState(Factory.Grounded());
            }
        }

        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}