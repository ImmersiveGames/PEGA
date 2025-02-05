using ImmersiveGames.HierarchicalStateMachine;
using UnityEngine;

namespace PEGA.ObjectSystems.MovementSystems.States
{
    public class DashState : BaseState
    {
        private float _dashTime;
        private readonly AnimatorHandler _animator;

        public DashState(MovementContext currentMovementContext, StateFactory factory) : base(currentMovementContext, factory)
        {
            _animator = currentMovementContext.GetComponent<AnimatorHandler>();
        }

        protected override StatesNames StateName => StatesNames.Dash;

        protected internal override void EnterState()
        {
            _animator.SetBool("Dash", true);
            base.EnterState();
            Ctx.isDashing = true;
            _dashTime = Ctx.movementSettings.dashDuration; // 🔹 Tempo do dash inicializado corretamente
        }

        protected override void UpdateState()
        {
            Ctx.ApplyMovement(Ctx.movementSettings.dashMultiply); // 🔹 Dash usa um multiplicador de velocidade
            Ctx.TimeInDash += Time.deltaTime;
            _dashTime -= Time.deltaTime;

            // 🔹 Garante que _dashTime nunca fique negativo
            if (_dashTime < 0) _dashTime = 0;

            Debug.Log($"Dash Time: {_dashTime}");
            CheckSwitchState(); // 🔹 Sempre chamar no final
        }

        protected override void ExitState()
        {
            _animator.SetBool("Dash", false);
            Ctx.isDashing = false;
            Ctx.TimeInDash = 0f; // 🔹 Reseta o tempo de Dash ao sair do estado
            _dashTime = 0f; // 🔹 Garante que o tempo seja zerado ao sair do estado
            base.ExitState();
        }

        public override void CheckSwitchState()
        {
            // 🔹 Sai do dash se o jogador soltar o botão ou o tempo acabar
            if (!Ctx.MovementDriver.IsDashPress || _dashTime <= 0)
            {
                if (!Ctx.CharacterController.isGrounded)
                {
                    SwitchState(Factory.Fall()); // 🔹 Se não está no chão, cai!
                }
                else
                {
                    CurrentSuperstate.SwitchSubState(Ctx.movementDirection == Vector2.zero ? Factory.Idle() : Factory.Walk());
                }
            }
        }

        public override void InitializeSubState()
        {
        }
    }
}