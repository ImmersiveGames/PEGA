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

        public DashState(MovementContext currentMovementContext, MovementStateFactory factory) : base(currentMovementContext, factory)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
            _ctx = currentMovementContext;
            _factory = factory;
        }

        public override StatesNames StateName => StatesNames.Dash;

        protected internal override void EnterState()
        {
            _animator.SetBool("Dash", true);
            _ctx.isDashing = true;
            _ctx.TimeInDash = 0f;
            _countdown = new CountdownManager();
            _countdown.RegisterCountdown(StatesNames.Dash.ToString(),_ctx.movementSettings.dashDuration, CheckSwitchState);
            base.EnterState();
            _dashDirection = _ctx.InputDriver.GetMovementDirection();
            // 🔹 Usa a direção do input se estiver se movendo
            if (_ctx.InputDriver.GetMovementDirection() == Vector2.zero)
            {
                var forward = _ctx.transform.forward.normalized * _ctx.movementSettings.idleDashMultiply;
                _dashDirection = new Vector2(forward.x, forward.z); // 🔹 Se parado, move para frente
            }
            
            //Debug
            _startPosition = _ctx.transform.position; // 🔹 Armazena a posição inicial do dash
        }

        protected override void UpdateState()
        {
            _ctx.TimeInDash += Time.deltaTime; //debug only
            _ctx.ApplyMovement(_dashDirection,_ctx.movementSettings.dashMultiply);
            _countdown.Update(Time.deltaTime);
            //base.UpdateState() // Aqui esta comentado porque o contador vai disparar a saida automaticamente
        }

        public override void ExitState()
        {
            _animator.SetBool("Dash", false);
            _ctx.isDashing = false;
            // 🔹 Inicia o cooldown ao final do Dash
            _ctx.DashingCooldown = true;

            DebugManager.Log<DashState>($"Dash Finalizado -> Cooldown Iniciado: {_ctx.movementSettings.dashDuration:F2}s");


            //######################
            var endPosition = _ctx.transform.position; // 🔹 Captura a posição final
            var distanceTraveled = Vector3.Distance(_startPosition, endPosition); // 🔹 Calcula a distância percorrida

            var finalMomentum = _ctx.CharacterController.velocity; // 🔹 Captura a velocidade final

            DebugManager.Log<DashState>($"Dash Finalizado -> Tempo: {_ctx.TimeInDash:F2}s, Distância: {distanceTraveled:F2}m, Momentum Final: {finalMomentum}");
            //######################
            
            base.ExitState();
        }

        protected override void CheckSwitchState()
        {
            Debug.Log("Dash Testando");
            if (!_ctx.CharacterController.isGrounded)
            {
                SwitchState(_factory.GetState(StatesNames.Fall));
            }
            else
            {
                CurrentSuperstate.SwitchSubState(_ctx.InputDriver.GetMovementDirection() == Vector2.zero ? _factory.GetState(StatesNames.Idle) : _factory.GetState(StatesNames.Walk));
            }
        }
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}
