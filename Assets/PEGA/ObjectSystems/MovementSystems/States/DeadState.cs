using ImmersiveGames.HierarchicalStateMachine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class DeadState: BaseState
    {
        public DeadState(StateContext currentMovementContext, HsmFactory factory) : base(currentMovementContext, factory)
        {
            IsRootState = true;
        }

        protected internal override void EnterState()
        {
            base.EnterState();
            Ctx.GlobalNotifyStateEnter(StatesNames.Dead);
        }

        public override StatesNames StateName => StatesNames.Dead;
        protected override void UpdateState()
        {
            //throw new System.NotImplementedException();
        }

        public override void CheckSwitchState()
        {
            //throw new System.NotImplementedException();
        }
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}