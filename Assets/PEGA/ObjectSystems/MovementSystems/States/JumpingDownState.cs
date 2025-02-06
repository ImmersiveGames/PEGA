using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class JumpingDownState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Dawn;
        public JumpingDownState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext, factory)
        {
            IsRootState = true;
        }

        protected internal override void EnterState()
        {
            Ctx.CalculateJumpVariables();
            base.EnterState();
        }

        protected override void UpdateState()
        {
            Ctx.ApplyGravity(falling:true);
        }

        protected override void ExitState()
        {
            Ctx.isJumping = false;
            base.ExitState();

            //######## DEBUG
            var jumpDuration = Time.time - Ctx.jumpStartTime;
            var finalPosition = Ctx.transform.position;
            var horizontalDistance = Vector3.Distance(
                new Vector3(Ctx.jumpStartPosition.x, 0, Ctx.jumpStartPosition.z),
                new Vector3(finalPosition.x, 0, finalPosition.z)
            );
            Debug.Log($"🛬 Jump Ended | Max Height: {Ctx.maxJumpHeight:F2}m | Distance: {horizontalDistance:F2}m | Time: {jumpDuration:F2}s");
            //######## 
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