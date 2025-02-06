using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class IdleState: BaseState
    {
        protected override StatesNames StateName => StatesNames.Idle;
        private readonly AnimatorHandler _animator;
        public IdleState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext,factory)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
        }

        protected internal override void EnterState()
        {
            _animator.SetFloat("Movement", 0);
            _animator.SetFloat("Idle", 0);
            Ctx.isWalking = false;
            Ctx.appliedMovement.x = 0;
            Ctx.appliedMovement.z = 0;
            base.EnterState();
        }

        protected override void UpdateState()
        {
            DebugManager.Log<IdleState>($"Update - Idle");
            CheckSwitchState(); //Subs Precisam atualizar aqui. E sempre no fim.
        }

        public override void CheckSwitchState()
        {
            if (!Ctx.CanDashAgain && !Ctx.MovementDriver.IsDashPress)
            {
                Ctx.CanDashAgain = true;
            }
            if (Ctx.CharacterController.isGrounded && Ctx.MovementDriver.IsDashPress && !Ctx.isDashing && Ctx.CanDashAgain)
            {
                Debug.Log("Dashing - Initialize - Do Idle");
                Ctx.CanDashAgain = false;
                //Aqui acho que é importante ele manda o Estado Acima, mudar.
                CurrentSuperstate.SwitchSubState(Factory.Dash());
                return;
            }
            
            if (Ctx.movementDirection != Vector2.zero)
            {
                CurrentSuperstate.SwitchSubState(Factory.Walk());
            }
        } 
        
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}