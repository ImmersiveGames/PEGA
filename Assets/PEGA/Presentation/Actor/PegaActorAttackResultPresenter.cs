using System.Collections.Generic;
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
            _visibleResult = $"ATAQUE #{result.AttemptId} · {status}{DescribeImpact(result)}";
            _visibleUntilTimeSeconds = result.ResolvedAtTimeSeconds + resultVisibilitySeconds;
        }

        private static string DescribeImpact(PegaActorAttackResolution resolution)
        {
            if (!resolution.IsHit)
            {
                return string.Empty;
            }

            int incapacitatedCount = 0;
            int activeCount = 0;
            int alreadyIncapacitatedCount = 0;
            int protectedCount = 0;
            int droppedItemCount = 0;
            for (int index = 0; index < resolution.Contacts.Count; index++)
            {
                PegaActorImpactResolution impact = resolution.Contacts[index].ImpactResolution;
                droppedItemCount += impact.DroppedItemCount;
                switch (impact.Outcome)
                {
                    case PegaActorImpactOutcome.Incapacitated:
                        incapacitatedCount++;
                        break;
                    case PegaActorImpactOutcome.RemainedActive:
                        activeCount++;
                        break;
                    case PegaActorImpactOutcome.AlreadyIncapacitated:
                        alreadyIncapacitatedCount++;
                        break;
                    case PegaActorImpactOutcome.Protected:
                        protectedCount++;
                        break;
                }
            }

            var descriptions = new List<string>();
            if (incapacitatedCount > 0)
            {
                descriptions.Add($"CAIU {incapacitatedCount}");
            }

            if (activeCount > 0)
            {
                descriptions.Add($"ATIVO {activeCount}");
            }

            if (alreadyIncapacitatedCount > 0)
            {
                descriptions.Add($"JÁ CAÍDO {alreadyIncapacitatedCount}");
            }

            if (protectedCount > 0)
            {
                descriptions.Add($"PROTEGIDO {protectedCount}");
            }

            if (incapacitatedCount > 0 || activeCount > 0 || alreadyIncapacitatedCount > 0)
            {
                descriptions.Add($"ITENS CAÍRAM {droppedItemCount}");
            }

            return descriptions.Count > 0
                ? $" · {string.Join(", ", descriptions)}"
                : " · IMPACTO SEM RECEPTOR";
        }

#if UNITY_EDITOR
        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_visibleResult) ||
                Time.timeAsDouble > _visibleUntilTimeSeconds)
            {
                return;
            }

            GUI.color = _visibleResult.Contains("PROTEGIDO")
                ? new Color(1f, 0.75f, 0.2f)
                : _visibleResult.Contains("ACERTOU")
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
