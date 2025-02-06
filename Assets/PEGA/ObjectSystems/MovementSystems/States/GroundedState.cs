using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class GroundedState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Grounded;

        public GroundedState(MovementContext currentMovementContext, StateFactory factory) : base(
            currentMovementContext, factory)
        {
            IsRootState = true;
        }

        protected internal override void EnterState()
        {
            Ctx.isGrounded = true;
            base.EnterState();

            Ctx.movement.y = Ctx.movementSettings.gravityGround;
            Ctx.appliedMovement.y = Ctx.movementSettings.gravityGround;
        }

        protected override void UpdateState()
        {
            //Debug.Log($"Subindo; {Ctx.movement.y}");
            //Debug.Log($"Update - Grounded - no chão: {Ctx.CharacterController.isGrounded}");
        }

        protected override void ExitState()
        {
            Ctx.isGrounded = false;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            //Verifica se ja soltou o botão para liberar o pulo
            if (!Ctx.CanJumpAgain && !Ctx.MovementDriver.IsJumpingPress)
            {
                Ctx.CanJumpAgain = true;
            }

            //Debug.Log("Can Jump Again?: " + Ctx.CanJumpAgain);
            if (!Ctx.CharacterController.isGrounded)
            {
                //Queda de plataforma
                SwitchState(Factory.Fall());
                return;
            }

            if (!Ctx.CharacterController.isGrounded || !Ctx.MovementDriver.IsJumpingPress 
                                                    || Ctx.isJumping || !Ctx.CanJumpAgain) return;
            Ctx.CanJumpAgain = false;
            SwitchState(Factory.Jump());
        }

        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            SwitchSubState(Ctx.movementDirection == Vector2.zero ? Factory.Idle() : Factory.Walk());
        }

    }
}

