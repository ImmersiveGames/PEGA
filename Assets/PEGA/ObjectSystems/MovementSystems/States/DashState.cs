using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using ImmersiveGames.Utils;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class DashState : BaseState
    {
        private readonly AnimatorHandler _animator;
        private Vector3 _startPosition; // 🔹 Posição inicial do dash
        private Vector2 _dashDirection;
        private readonly MovementContext _ctx;
        private readonly MovementStateFactory _factory;
        private CountdownManager _countdown;
        
        private bool _isDashing;

        public DashState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
            _ctx = currentMovementContext;
            _factory = factory;
        }

        protected override StatesNames StateName => StatesNames.Dash;

        public override void OnEnter()
        {
            _animator.SetBool("Dash", true);
            _ctx.isDashing = true;
            _isDashing = true;
            _ctx.TimeInDash = 0f;
            _countdown = new CountdownManager();
            _countdown.RegisterCountdown(StatesNames.Dash.ToString(),_ctx.movementSettings.dashDuration, ()=>_isDashing = false);
            _dashDirection = _ctx.InputDriver.GetMovementDirection();
            // 🔹 Usa a direção do input se estiver se movendo
            if (_ctx.InputDriver.GetMovementDirection() == Vector2.zero)
            {
                var forward = _ctx.transform.forward.normalized * _ctx.movementSettings.idleDashMultiply;
                _dashDirection = new Vector2(forward.x, forward.z); // 🔹 Se parado, move para frente
            }
            base.OnEnter();
            
            _startPosition = _ctx.transform.position; // 🔹 Armazena a posição inicial do dash
        }

        public override void Tick()
        {
            _ctx.TimeInDash += Time.deltaTime; //debug only
            _ctx.ApplyMovement(_dashDirection,_ctx.movementSettings.dashMultiply);
            _countdown.Update(Time.deltaTime);
            base.Tick(); // Aqui está comentado porque o contador vai disparar a saida automaticamente
        }

        public override void OnExit()
        {
            _ctx.ApplyMovement(_ctx.InputDriver.GetMovementDirection());
            _animator.SetBool("Dash", false);
            // 🔹 Inicia o cooldown ao final do Dash
            _ctx.DashingCooldown = true;
            _ctx.isDashing = false;
            DebugManager.Log<DashState>($"Dash Finalizado -> Cooldown Iniciado: {_ctx.movementSettings.dashDuration:F2}s");


            //######################
            var endPosition = _ctx.transform.position; // 🔹 Captura a posição final
            var distanceTraveled = Vector3.Distance(_startPosition, endPosition); // 🔹 Calcula a distância percorrida

            var finalMomentum = _ctx.CharacterController.velocity; // 🔹 Captura a velocidade final

            DebugManager.Log<DashState>($"Dash Finalizado -> Tempo: {_ctx.TimeInDash:F2}s, Distância: {distanceTraveled:F2}m, Momentum Final: {finalMomentum}");
            //######################
            
            base.OnExit();
        }

        protected override void SetupTransitions()
        {
            
            AddTransition(_factory.GetState(StatesNames.Grounded), () => _ctx.CharacterController.isGrounded && !_isDashing);
        }
        
    }
}
