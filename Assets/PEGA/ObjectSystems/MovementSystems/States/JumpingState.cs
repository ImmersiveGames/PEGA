using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class JumpingState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Jump;
        private readonly AnimatorHandler _animator;
        public JumpingState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext, factory)
        {
            IsRootState = true;
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
        }

        protected internal override void EnterState()
        {
            // 📌 Armazena posição inicial e tempo do pulo no Contexto
            Ctx.jumpStartPosition = Ctx.transform.position;
            Ctx.maxJumpHeight = Ctx.jumpStartPosition.y;
            Ctx.jumpStartTime = Time.time;
            
            
            DebugManager.Log<JumpingState>($"🏃 Entering JumpState | TimeInDash={Ctx.TimeInDash} | Velocity={Ctx.StoredMomentum}");
            
            _animator.SetBool("Jump", true);
            Ctx.isJumping = true;
            Ctx.CalculateJumpVariables();
            HandleJump();
            base.EnterState();
        }

        protected override void UpdateState()
        {
            Ctx.ApplyGravity(falling:false);
        }

        protected override void ExitState()
        {
            Ctx.maxJumpHeight = Ctx.transform.position.y;
            _animator.SetBool("Jump", false);
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (Ctx.movement.y <= 0 || !Ctx.MovementDriver.IsJumpingPress)
            {
                SwitchState(Factory.Down());
            }
        }

        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
        private void HandleJump()
        {
            // 🔹 Usa a nova velocidade vertical calculada
            Ctx.movement.y = Ctx.initialJumpVelocity;
            Ctx.appliedMovement.y = Ctx.initialJumpVelocity;
        }

    }
}