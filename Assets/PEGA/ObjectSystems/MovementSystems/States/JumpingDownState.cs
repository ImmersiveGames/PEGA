using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class JumpingDownState : BaseState
    {
        protected override StatesNames StateName => StatesNames.Dawn;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;
        public JumpingDownState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext)
        {
            _ctx = currentMovementContext;
            _factory = factory;
        }

        protected internal override void OnEnter()
        {
            _ctx.CalculateJumpVariables();
            base.OnEnter();
        }

        protected override void Tick()
        {
            _ctx.ApplyGravity(falling:true);
            base.Tick();
        }

        protected override void OnExit()
        {
            _ctx.isJumping = false;
            base.OnExit();

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
                SwitchState(_factory.GetState(StatesNames.Dead));
                return;
            }
            if (_ctx.CharacterController.isGrounded)
            {
                SwitchState(_factory.GetState(StatesNames.Grounded));
            }
        }
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected sealed override void InitializeSubStatesOnEnter()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}