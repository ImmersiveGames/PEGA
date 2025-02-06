using ImmersiveGames.DebugSystems;
using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class DashState : BaseState
    {
        private float _dashTime;
        private readonly AnimatorHandler _animator;
        private Vector3 _startPosition; // 🔹 Posição inicial do dash
        private Vector2 _dashDirection;

        public DashState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext, factory)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
        }

        protected override StatesNames StateName => StatesNames.Dash;

        protected internal override void EnterState()
        {
            _animator.SetBool("Dash", true);
            Ctx.isDashing = true;
            _dashTime = Ctx.movementSettings.dashDuration;
            base.EnterState();
            
            // 📌 Guarda a direção inicial do Dash
            _dashDirection = Ctx.movementDirection; // 🔹 Usa a direção do input se estiver se movendo
            if (Ctx.movementDirection == Vector2.zero)
            {
                var forward = Ctx.transform.forward.normalized * Ctx.movementSettings.idleDashMultiply;
                _dashDirection = new Vector2(forward.x, forward.z); // 🔹 Se parado, move para frente
            }
            
            //Debug
            _startPosition = Ctx.transform.position; // 🔹 Armazena a posição inicial do dash
        }

        protected override void UpdateState()
        {
            // 📌 Força um movimento inicial se o jogador estiver parado (Idle)
            if (Ctx.movementDirection == Vector2.zero) Ctx.movementDirection = _dashDirection; // 🔹 Converte para um Vector2
            
            Ctx.ApplyMovement(Ctx.movementSettings.dashMultiply);
            Ctx.TimeInDash += Time.deltaTime;
            _dashTime -= Time.deltaTime;

            if (_dashTime < 0) _dashTime = 0;

            CheckSwitchState();
        }

        protected override void ExitState()
        {
            _animator.SetBool("Dash", false);
            Ctx.isDashing = false;
            // 🔹 Inicia o cooldown ao final do Dash
            Ctx.DashCooldownTimer = Ctx.movementSettings.dashCooldownTime;

            DebugManager.Log<DashState>($"Dash Finalizado -> Cooldown Iniciado: {Ctx.DashCooldownTimer:F2}s");


            //######################
            var endPosition = Ctx.transform.position; // 🔹 Captura a posição final
            var distanceTraveled = Vector3.Distance(_startPosition, endPosition); // 🔹 Calcula a distância percorrida

            var finalMomentum = Ctx.CharacterController.velocity; // 🔹 Captura a velocidade final

            DebugManager.Log<DashState>($"Dash Finalizado -> Tempo: {Ctx.TimeInDash:F2}s, Distância: {distanceTraveled:F2}m, Momentum Final: {finalMomentum}");
            //######################
            
            Ctx.TimeInDash = 0f;
            _dashTime = 0f;
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            if (Ctx.MovementDriver.IsDashPress && !(_dashTime <= 0)) return;
            if (!Ctx.CharacterController.isGrounded)
            {
                SwitchState(Factory.Fall());
            }
            else
            {
                CurrentSuperstate.SwitchSubState(Ctx.movementDirection == Vector2.zero ? Factory.Idle() : Factory.Walk());
            }
        }
        //Inicializa qual sub estado vai entrar "automaticamente ao entrar nesse estado e deve ser chamado no início"
        protected override void InitializeSubState()
        {
            //Nenhum Estado é inicializado junto a este estado
        }
    }
}
