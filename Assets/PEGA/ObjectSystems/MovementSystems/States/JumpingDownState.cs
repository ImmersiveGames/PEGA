using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class JumpingDownState : BaseState
    {
        public override StatesNames StateName => StatesNames.Dawn;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;
        public JumpingDownState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext, factory)
        {
            IsRootState = true;
            _ctx = currentMovementContext;
            _factory = factory;
        }

        protected internal override void EnterState()
        {
            _ctx.CalculateJumpVariables();
            base.EnterState();
        }

        protected override void UpdateState()
        {
            _ctx.ApplyGravity(falling:true);
            base.UpdateState();
        }

        public override void ExitState()
        {
            _ctx.isJumping = false;
            base.ExitState();

            //######## DEBUG
            var jumpDuration = Time.time - _ctx.jumpStartTime;
            var finalPosition = _ctx.transform.position;
            var horizontalDistance = Vector3.Distance(
                new Vector3(_ctx.jumpStartPosition.x, 0, _ctx.jumpStartPosition.z),
                new Vector3(finalPosition.x, 0, finalPosition.z)
            );
            DebugManager.Log<JumpingDownState>($"🛬 Jump Ended | Max Height: {_ctx.maxJumpHeight:F2}m | Distance: {horizontalDistance:F2}m | Time: {jumpDuration:F2}s");
            //######## 
        }

        protected override void CheckSwitchState()
        {
            if (_ctx.transform.position.y < _ctx.fallMaxHeight)
            {
                SwitchState(Factory.GetState(StatesNames.Dead));
                return;
            }
            if (_ctx.CharacterController.isGrounded)
            {
                SwitchState(_factory.GetState(StatesNames.Grounded));
            }
        }
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}