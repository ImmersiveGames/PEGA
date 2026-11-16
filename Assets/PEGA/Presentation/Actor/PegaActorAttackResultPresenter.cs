using Immersive.Foundation.Events;
using PEGA.Attacks.Runtime;
using UnityEngine;

namespace PEGA.Presentation.Actor
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PegaActorAttackMechanic))]
    [AddComponentMenu("PEGA/Presentation/Actor/Attack Result Feedback")]
    public sealed class PegaActorAttackResultPresenter : MonoBehaviour
    {
        [SerializeField, Min(0.1f)]
        [Tooltip("Tempo em segundos que o resultado fica visível na Game View do Unity Editor.")]
        private float resultVisibilitySeconds = 1.5f;

        private PegaActorAttackMechanic _attackMechanic;
        private IEventBinding _attemptBinding;
        private IEventBinding _resolutionBinding;
        private string _visibleResult = string.Empty;
        private double _visibleUntilTimeSeconds;

        private void OnEnable()
        {
            _attackMechanic = GetComponent<PegaActorAttackMechanic>();
            if (_attackMechanic == null)
            {
                enabled = false;
                return;
            }

            _attemptBinding = _attackMechanic.SubscribeToAttempts(OnAttackAttempted);
            _resolutionBinding = _attackMechanic.SubscribeToResolutions(OnAttackResolved);
        }

        private void OnDisable()
        {
            if (_attackMechanic != null)
            {
                if (_attemptBinding != null)
                {
                    _attackMechanic.UnsubscribeFromAttempts(_attemptBinding);
                    _attemptBinding = null;
                }

                if (_resolutionBinding != null)
                {
                    _attackMechanic.UnsubscribeFromResolutions(_resolutionBinding);
                    _resolutionBinding = null;
                }
            }

            _visibleResult = string.Empty;
        }

        private void OnAttackAttempted(PegaActorAttackAttemptedEvent attempted)
        {
            _visibleResult = $"TENTATIVA #{attempted.AttemptId}";
            _visibleUntilTimeSeconds = attempted.AttemptedAtTimeSeconds + resultVisibilitySeconds;
        }

        private void OnAttackResolved(PegaActorAttackResolvedEvent resolved)
        {
            PegaActorAttackResolution result = resolved.Resolution;
            string status = result.Result switch
            {
                PegaActorAttackResultKind.Hit => $"ACERTOU ({result.Contacts.Count})",
                PegaActorAttackResultKind.Miss => "ERROU",
                PegaActorAttackResultKind.Unavailable => "SEM CONSULTA DE ACTOR",
                _ => "RESULTADO INVÁLIDO"
            };
            _visibleResult = $"ATAQUE #{result.AttemptId} · {status}";
            _visibleUntilTimeSeconds = result.ResolvedAtTimeSeconds + resultVisibilitySeconds;
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_visibleResult) ||
                Time.timeAsDouble > _visibleUntilTimeSeconds)
            {
                return;
            }

            GUI.color = _visibleResult.Contains("ACERTOU")
                ? Color.green
                : _visibleResult.Contains("ERROU")
                    ? Color.red
                    : Color.white;
            GUI.Label(
                new Rect(16f, 16f, 420f, 28f),
                $"{gameObject.name} · {_visibleResult}");
            GUI.color = Color.white;
        }
#endif
    }
}
