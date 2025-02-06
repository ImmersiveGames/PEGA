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
            Debug.Log($"🏃 Entering JumpState | TimeInDash={Ctx.TimeInDash}");
            
            
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
            
            /*if (Ctx.MovementDriver.IsDashPress && !Ctx.isDashing && Ctx.dashCooldown <= 0)
            {
                Debug.Log("Dashing - Initialize - Do Jumping");
                SwitchSubState(Factory.Dash());
                return;
            }*/
            /*if (Ctx.movementDirection == Vector2.zero )
            {
                SwitchSubState(Factory.Idle());
            }
            else
            {
                SwitchSubState(Factory.Walk());
            }*/
        }
        private void HandleJump()
        {
            /*var horizontalSpeed = Ctx.StoredMomentum.magnitude;

            // 🔹 Define um multiplicador para controlar a influência do Dash no impulso inicial do pulo
            var dashJumpInfluence = Mathf.Lerp(Ctx.movementSettings.minDashJumpInfluence, Ctx.movementSettings.maxDashJumpInfluence, Ctx.TimeInDash / Ctx.movementSettings.dashDuration);

            // 🔹 Aplica o impulso do Dash no pulo, mas limita o efeito
            var impulsoFinal = horizontalSpeed * dashJumpInfluence;
            var maxJumpBoost = Ctx.movementSettings.maxJumpHeight * Ctx.movementSettings.momentumMultiply;
            impulsoFinal = Mathf.Min(impulsoFinal, maxJumpBoost);

            // 🔹 Agora, initialJumpVelocity já foi atualizado em `CalculateJumpVariables()`
            Ctx.movement.y = Ctx.initialJumpVelocity + impulsoFinal;
            Ctx.appliedMovement.y = Ctx.movement.y;*/
            
            Ctx.movement.y = Ctx.initialJumpVelocity;
            Ctx.appliedMovement.y = Ctx.initialJumpVelocity;
        }

    }
}