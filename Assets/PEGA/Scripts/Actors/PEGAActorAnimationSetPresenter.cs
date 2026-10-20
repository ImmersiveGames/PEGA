using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Actors
{
    /// <summary>
    /// Conjunto de animações de um personagem, entregue como parte de composição de chave
    /// <c>animation</c> (dependente de <c>visual</c>) no mesmo mount do Actor.
    /// Aplica o controlador do conjunto ao Animator do visual atual e o alimenta com os
    /// sinais publicados pela raiz do Actor. Reencontra o Animator quando o visual é trocado.
    /// Destination: Production.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PEGAActorAnimationSetPresenter : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Presentation.Animation";

        private static readonly int LocomotionParameter = Animator.StringToHash("Locomotion");
        private static readonly int TurnAngleParameter = Animator.StringToHash("TurnAngle");
        private static readonly int TurnParameter = Animator.StringToHash("Turn");
        private static readonly int TurnPlaybackSpeedParameter = Animator.StringToHash("TurnPlaybackSpeed");
        private static readonly int TurnStateTag = Animator.StringToHash("Turn");

        [Header("Animation Set")]
        [SerializeField]
        [Tooltip("Controlador base do PEGA ou um Animator Override Controller dele com os clipes deste personagem.")]
        private RuntimeAnimatorController animationSet;

        [Header("Locomotion Presentation")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Velocidade planar em que o clipe de andar toca puro (m/s). Abaixo dela, mistura com parado.")]
        private float walkSpeedMetersPerSecond = 1.5f;

        [SerializeField, Min(0.02f)]
        [Tooltip("Velocidade planar em que o clipe de correr toca puro (m/s). Entre andar e correr, os dois se misturam. Deve ser maior que a de andar.")]
        private float runSpeedMetersPerSecond = 3.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Tempo de suavização do parâmetro Locomotion enviado ao Animator (s).")]
        private float locomotionDampTimeSeconds = 0.1f;

        [Header("Turn In Place Presentation")]
        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Velocidade mínima de reprodução do clipe de giro (x). O clipe é acelerado ou desacelerado para terminar junto com o giro do corpo.")]
        private float minimumTurnPlaybackSpeed = 0.5f;

        [SerializeField, Range(1f, 10f)]
        [Tooltip("Velocidade máxima de reprodução do clipe de giro (x).")]
        private float maximumTurnPlaybackSpeed = 5f;

        private PEGAActorPresentationSignals _signals;
        private Animator _animator;
        private int _consumedTurnSequence;
        private bool _turnTriggerPending;
        private bool _turnPlaybackPending;
        private float _requestedTurnAngleDegrees;
        private bool _reportedMissingAnimator;
        private bool _configurationFailed;
        private ScopedLogger _log;

        private void Awake()
        {
            _log = PEGALogging.CreateScopedLogger<PEGAActorAnimationSetPresenter>(
                LogCategory,
                gameObject.name);
        }

        private void OnEnable()
        {
            _signals = GetComponentInParent<PEGAActorPresentationSignals>();
            _animator = null;
            _reportedMissingAnimator = false;
            _configurationFailed = false;

            if (animationSet == null)
            {
                FailConfiguration($"{nameof(PEGAActorAnimationSetPresenter)} requires an animation set controller.");
                return;
            }

            if (_signals == null)
            {
                FailConfiguration($"{nameof(PEGAActorAnimationSetPresenter)} requires a parent {nameof(PEGAActorPresentationSignals)} on the Actor root.");
                return;
            }

            if (minimumTurnPlaybackSpeed > maximumTurnPlaybackSpeed)
            {
                FailConfiguration($"{nameof(PEGAActorAnimationSetPresenter)} requires the minimum turn playback speed to be at most the maximum.");
                return;
            }

            if (!IsFinitePositive(walkSpeedMetersPerSecond) ||
                !IsFinitePositive(runSpeedMetersPerSecond) ||
                runSpeedMetersPerSecond <= walkSpeedMetersPerSecond)
            {
                FailConfiguration($"{nameof(PEGAActorAnimationSetPresenter)} requires positive walk and run speeds, with run above walk.");
                return;
            }

            _consumedTurnSequence = _signals.TurnRequestSequence;
            TryBindAnimator();
        }

        /// <summary>0 = parado, 1 = andar, 2 = correr; linear entre os pontos calibrados.</summary>
        private float ResolveLocomotionBlend(float planarSpeed)
        {
            if (planarSpeed <= walkSpeedMetersPerSecond)
            {
                return Mathf.Max(0f, planarSpeed / walkSpeedMetersPerSecond);
            }

            float runBlend = (planarSpeed - walkSpeedMetersPerSecond) /
                (runSpeedMetersPerSecond - walkSpeedMetersPerSecond);
            return 1f + Mathf.Min(1f, runBlend);
        }

        private void LateUpdate()
        {
            if (_configurationFailed)
            {
                return;
            }

            if ((_animator == null || !_animator.isActiveAndEnabled) && !TryBindAnimator())
            {
                return;
            }

            _animator.SetFloat(
                LocomotionParameter,
                ResolveLocomotionBlend(_signals.PlanarSpeedMetersPerSecond),
                locomotionDampTimeSeconds,
                Time.deltaTime);

            // O Animator avalia o trigger no update seguinte; um pedido não consumido
            // não deve disparar um giro atrasado.
            if (_turnTriggerPending)
            {
                _turnTriggerPending = false;
                _animator.ResetTrigger(TurnParameter);
            }

            if (_turnPlaybackPending)
            {
                ApplyTurnPlaybackSpeed();
            }

            if (_signals.TurnRequestSequence != _consumedTurnSequence)
            {
                _consumedTurnSequence = _signals.TurnRequestSequence;
                _requestedTurnAngleDegrees = _signals.LastTurnAngleDegrees;
                _animator.SetFloat(TurnAngleParameter, _requestedTurnAngleDegrees);
                _animator.SetTrigger(TurnParameter);
                _turnTriggerPending = true;
                _turnPlaybackPending = true;
            }
        }

        /// <summary>
        /// Ajusta a reprodução do clipe de giro que o Animator acabou de escolher para que ele
        /// dure o mesmo tempo que o gameplay leva para girar o corpo pelo ângulo pedido.
        /// </summary>
        private void ApplyTurnPlaybackSpeed()
        {
            AnimatorClipInfo[] clips;
            if (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).tagHash == TurnStateTag)
            {
                clips = _animator.GetNextAnimatorClipInfo(0);
            }
            else if (_animator.GetCurrentAnimatorStateInfo(0).tagHash == TurnStateTag)
            {
                clips = _animator.GetCurrentAnimatorClipInfo(0);
            }
            else
            {
                // O giro ainda não começou; o pedido não consumido é descartado pelo reset do trigger.
                _turnPlaybackPending = _turnTriggerPending;
                return;
            }

            _turnPlaybackPending = false;
            float turnRate = _signals.BodyTurnRateDegreesPerSecond;
            float bodyTurnSeconds = turnRate > 0f ? Mathf.Abs(_requestedTurnAngleDegrees) / turnRate : 0f;
            if (clips.Length == 0 || clips[0].clip == null || bodyTurnSeconds <= 0f)
            {
                _animator.SetFloat(TurnPlaybackSpeedParameter, 1f);
                return;
            }

            float playbackSpeed = Mathf.Clamp(
                clips[0].clip.length / bodyTurnSeconds,
                minimumTurnPlaybackSpeed,
                maximumTurnPlaybackSpeed);
            _animator.SetFloat(TurnPlaybackSpeedParameter, playbackSpeed);
        }

        private bool TryBindAnimator()
        {
            Animator candidate = FindVisualAnimator(out int candidateCount);
            if (candidateCount > 1)
            {
                FailConfiguration($"{nameof(PEGAActorAnimationSetPresenter)} found more than one active Animator beside it in the composition mount.");
                return false;
            }

            if (candidate == null)
            {
                _animator = null;
                if (!_reportedMissingAnimator)
                {
                    _reportedMissingAnimator = true;
                    _log.Warning(
                        "Animation set is waiting for an active visual Animator in its composition mount.",
                        LogFields.Field("animationPart", gameObject.name));
                }

                return false;
            }

            if (candidate.applyRootMotion)
            {
                FailConfiguration($"{nameof(PEGAActorAnimationSetPresenter)} requires Animator Root Motion to be disabled; physical movement belongs to {nameof(PEGAActorMovementMechanic)}.");
                return false;
            }

            candidate.runtimeAnimatorController = animationSet;
            if (!HasParameter(candidate, LocomotionParameter, AnimatorControllerParameterType.Float) ||
                !HasParameter(candidate, TurnAngleParameter, AnimatorControllerParameterType.Float) ||
                !HasParameter(candidate, TurnParameter, AnimatorControllerParameterType.Trigger) ||
                !HasParameter(candidate, TurnPlaybackSpeedParameter, AnimatorControllerParameterType.Float))
            {
                FailConfiguration($"{nameof(PEGAActorAnimationSetPresenter)} requires Animator parameters 'Locomotion' (Float), 'TurnAngle' (Float), 'Turn' (Trigger) and 'TurnPlaybackSpeed' (Float).");
                return false;
            }

            _animator = candidate;
            _reportedMissingAnimator = false;
            _turnTriggerPending = false;
            _consumedTurnSequence = _signals.TurnRequestSequence;
            _log.Info(
                "Animation set bound to visual Animator.",
                LogFields.Field("animationPart", gameObject.name),
                LogFields.Field("actorRoot", _signals.gameObject.name),
                LogFields.Field("animator", candidate.name),
                LogFields.Field("animationSet", animationSet.name),
                LogFields.Field("walkSpeedMps", walkSpeedMetersPerSecond),
                LogFields.Field("runSpeedMps", runSpeedMetersPerSecond));
            return true;
        }

        private Animator FindVisualAnimator(out int candidateCount)
        {
            candidateCount = 0;
            Transform mount = transform.parent;
            if (mount == null)
            {
                return null;
            }

            Animator found = null;
            for (int index = 0; index < mount.childCount; index++)
            {
                Transform sibling = mount.GetChild(index);
                if (sibling == transform || !sibling.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Animator animator = sibling.GetComponentInChildren<Animator>(false);
                if (animator == null || !animator.enabled)
                {
                    continue;
                }

                candidateCount++;
                found = animator;
            }

            return found;
        }

        private void FailConfiguration(string message)
        {
            _configurationFailed = true;
            _animator = null;
            _log.Error(
                message,
                LogFields.Field("animationPart", gameObject.name),
                LogFields.Field("animationSet", animationSet != null ? animationSet.name : "unassigned"));
        }

        private static bool IsFinitePositive(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool HasParameter(
            Animator animator,
            int parameterHash,
            AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == parameterHash)
                {
                    return parameter.type == type;
                }
            }

            return false;
        }
    }
}
