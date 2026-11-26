using System;
using Immersive.Foundation.Events;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Actors.Runtime;
using PEGA.Attacks.Runtime;
using PEGA.Diagnostics;
using PEGA.Movement.Actor;
using UnityEngine;
namespace PEGA.Presentation.Actor
{
    /// <summary>
    /// Conjunto de animações de um personagem, entregue como parte de composição de chave
    /// <c>animation</c> (dependente de <c>visual</c>) no mesmo mount do Actor.
    /// Aplica o controlador do conjunto ao Animator do visual atual e o alimenta com os
    /// sinais publicados pela raiz do Actor. Reencontra o Animator quando o visual é trocado.
    /// Destination: Production.
    /// </summary>
    [DisallowMultipleComponent]
        [AddComponentMenu("PEGA/Presentation/Actor/Animation Set Presenter")]
    public sealed class PegaActorAnimationSetPresenter : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Presentation.Animation";

        private static readonly int LocomotionParameter = Animator.StringToHash("Locomotion");
        private static readonly int TurnAngleParameter = Animator.StringToHash("TurnAngle");
        private static readonly int TurnParameter = Animator.StringToHash("Turn");
        private static readonly int TurnPlaybackSpeedParameter = Animator.StringToHash("TurnPlaybackSpeed");
        private static readonly int TurnStateTag = Animator.StringToHash("Turn");
        private static readonly int AttackStateTag = Animator.StringToHash("Attack");
        private static readonly int FallStateTag = Animator.StringToHash("Fall");

        [Header("Animation Set")]
        [SerializeField]
        [Tooltip("Controlador base do PEGA ou um Animator Override Controller dele com os clipes deste personagem.")]
        private RuntimeAnimatorController animationSet;

        [Header("Attack Presentation")]
        [SerializeField]
        [Tooltip("Nome do parâmetro Trigger do Animator que inicia a animação de ataque deste Actor.")]
        private string attackTriggerParameterName = "Attack";

        [Header("Incapacitation Presentation")]
        [SerializeField]
        [Tooltip("Nome do parâmetro Trigger do Animator que inicia a animação de queda deste Actor.")]
        private string fallTriggerParameterName = "Fall";

        [SerializeField]
        [Tooltip("Nome do parâmetro Trigger do Animator que retoma a animação de locomoção após a recuperação.")]
        private string recoverTriggerParameterName = "Recover";

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

        private PegaActorPresentationSignals _signals;
        private PegaActorAttackMechanic _attackMechanic;
        private PegaActorIncapacitationMechanic _incapacitationMechanic;
        private IEventBinding _attackAttemptBinding;
        private IEventBinding _incapacitationBinding;
        private Animator _animator;
        private int _attackTriggerParameter;
        private int _fallTriggerParameter;
        private int _recoverTriggerParameter;
        private bool _hasValidAttackTrigger;
        private bool _hasValidIncapacitationTriggers;
        private bool _hasPresentedIncapacitationState;
        private bool _lastPresentedIncapacitated;
        private bool _fallPresentationPending;
        private int _consumedTurnSequence;
        private bool _turnTriggerPending;
        private bool _turnPlaybackPending;
        private float _requestedTurnAngleDegrees;
        private bool _reportedMissingAnimator;
        private bool _configurationFailed;
        private ScopedLogger _log;

        private void Awake()
        {
            _log = PegaLogging.CreateScopedLogger<PegaActorAnimationSetPresenter>(
                LogCategory,
                gameObject.name);
        }

        private void OnEnable()
        {
            _signals = GetComponentInParent<PegaActorPresentationSignals>();
            _attackMechanic = GetComponentInParent<PegaActorAttackMechanic>();
            _incapacitationMechanic = GetComponentInParent<PegaActorIncapacitationMechanic>();
            _animator = null;
            _hasValidAttackTrigger = false;
            _hasValidIncapacitationTriggers = false;
            _hasPresentedIncapacitationState = false;
            _fallPresentationPending = false;
            _reportedMissingAnimator = false;
            _configurationFailed = false;

            if (animationSet == null)
            {
                FailConfiguration($"{nameof(PegaActorAnimationSetPresenter)} requires an animation set controller.");
                return;
            }

            if (_incapacitationMechanic != null && !HasDistinctIncapacitationTriggerNames())
            {
                FailConfiguration("Fall and Recover Animator triggers must be non-empty and distinct from each other, Attack and Turn.");
                return;
            }

            if (_signals == null)
            {
                FailConfiguration($"{nameof(PegaActorAnimationSetPresenter)} requires a parent {nameof(PegaActorPresentationSignals)} on the Actor root.");
                return;
            }

            if (minimumTurnPlaybackSpeed > maximumTurnPlaybackSpeed)
            {
                FailConfiguration($"{nameof(PegaActorAnimationSetPresenter)} requires the minimum turn playback speed to be at most the maximum.");
                return;
            }

            if (!IsFinitePositive(walkSpeedMetersPerSecond) ||
                !IsFinitePositive(runSpeedMetersPerSecond) ||
                runSpeedMetersPerSecond <= walkSpeedMetersPerSecond)
            {
                FailConfiguration($"{nameof(PegaActorAnimationSetPresenter)} requires positive walk and run speeds, with run above walk.");
                return;
            }

            _consumedTurnSequence = _signals.TurnRequestSequence;
            TryBindAnimator();

            if (_attackMechanic != null)
            {
                _attackAttemptBinding = _attackMechanic.SubscribeToAttempts(OnAttackAttempted);
            }

            if (_incapacitationMechanic != null)
            {
                _incapacitationBinding = _incapacitationMechanic.SubscribeToIncapacitationChanges(
                    OnIncapacitationChanged);
            }
        }

        private void OnDisable()
        {
            if (_attackMechanic != null && _attackAttemptBinding != null)
            {
                _attackMechanic.UnsubscribeFromAttempts(_attackAttemptBinding);
            }

            if (_incapacitationMechanic != null && _incapacitationBinding != null)
            {
                _incapacitationMechanic.UnsubscribeFromIncapacitationChanges(_incapacitationBinding);
            }

            _attackAttemptBinding = null;
            _incapacitationBinding = null;
            _attackMechanic = null;
            _incapacitationMechanic = null;
            _hasValidAttackTrigger = false;
            _hasValidIncapacitationTriggers = false;
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

            bool isIncapacitated =
                _incapacitationMechanic != null && _incapacitationMechanic.IsIncapacitated;
            if (_incapacitationMechanic != null && !_hasPresentedIncapacitationState)
            {
                if (isIncapacitated)
                {
                    PresentIncapacitationState(true);
                }
                else if (IsFallStateActiveOrEntering())
                {
                    PresentIncapacitationState(false);
                }
                else
                {
                    _lastPresentedIncapacitated = false;
                    _hasPresentedIncapacitationState = true;
                }
            }
            else if (_incapacitationMechanic != null &&
                _lastPresentedIncapacitated != isIncapacitated)
            {
                PresentIncapacitationState(isIncapacitated);
            }

            if (isIncapacitated)
            {
                if (_fallPresentationPending && _hasValidIncapacitationTriggers)
                {
                    if (IsFallStateActiveOrEntering())
                    {
                        _fallPresentationPending = false;
                    }
                    else
                    {
                        _animator.ResetTrigger(_fallTriggerParameter);
                        _animator.SetTrigger(_fallTriggerParameter);
                    }
                }

                _animator.SetFloat(
                    LocomotionParameter,
                    0f,
                    locomotionDampTimeSeconds,
                    Time.deltaTime);
                _animator.ResetTrigger(TurnParameter);
                _turnTriggerPending = false;
                _turnPlaybackPending = false;
                _consumedTurnSequence = _signals.TurnRequestSequence;
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

        private void OnAttackAttempted(PegaActorAttackAttemptedEvent attempted)
        {
            if (!isActiveAndEnabled || _configurationFailed || _attackMechanic == null)
            {
                return;
            }

            if ((_animator == null || !_animator.isActiveAndEnabled) && !TryBindAnimator())
            {
                return;
            }

            if (!_hasValidAttackTrigger)
            {
                return;
            }

            if (IsAttackStateActiveOrEntering())
            {
                _animator.ResetTrigger(_attackTriggerParameter);
                return;
            }

            _animator.ResetTrigger(_attackTriggerParameter);
            _animator.SetTrigger(_attackTriggerParameter);
        }

        private void OnIncapacitationChanged(PegaActorIncapacitationChangedEvent changed)
        {
            if (!isActiveAndEnabled || _configurationFailed || changed == null ||
                _signals == null || changed.ActorRoot != _signals.gameObject)
            {
                return;
            }

            PresentIncapacitationState(changed.IsIncapacitated);
        }

        private void PresentIncapacitationState(bool isIncapacitated)
        {
            if (_hasPresentedIncapacitationState &&
                _lastPresentedIncapacitated == isIncapacitated)
            {
                return;
            }

            if ((_animator == null || !_animator.isActiveAndEnabled) && !TryBindAnimator())
            {
                return;
            }

            if (!_hasValidIncapacitationTriggers)
            {
                _lastPresentedIncapacitated = isIncapacitated;
                _hasPresentedIncapacitationState = true;
                return;
            }

            if (isIncapacitated)
            {
                if (_hasValidAttackTrigger)
                {
                    _animator.ResetTrigger(_attackTriggerParameter);
                }
                _animator.ResetTrigger(TurnParameter);
                _animator.ResetTrigger(_recoverTriggerParameter);
                _turnTriggerPending = false;
                _turnPlaybackPending = false;
                _consumedTurnSequence = _signals.TurnRequestSequence;
                if (IsFallStateActiveOrEntering())
                {
                    _lastPresentedIncapacitated = true;
                    _hasPresentedIncapacitationState = true;
                    return;
                }

                _animator.ResetTrigger(_fallTriggerParameter);
                _animator.SetTrigger(_fallTriggerParameter);
                _fallPresentationPending = true;
            }
            else
            {
                if (_hasValidAttackTrigger)
                {
                    _animator.ResetTrigger(_attackTriggerParameter);
                }
                _animator.ResetTrigger(TurnParameter);
                _animator.ResetTrigger(_fallTriggerParameter);
                _turnTriggerPending = false;
                _turnPlaybackPending = false;
                _consumedTurnSequence = _signals.TurnRequestSequence;
                _fallPresentationPending = false;
                _animator.ResetTrigger(_recoverTriggerParameter);
                _animator.SetTrigger(_recoverTriggerParameter);
            }

            _lastPresentedIncapacitated = isIncapacitated;
            _hasPresentedIncapacitationState = true;
        }

        private bool HasDistinctIncapacitationTriggerNames()
        {
            if (string.IsNullOrWhiteSpace(fallTriggerParameterName) ||
                string.IsNullOrWhiteSpace(recoverTriggerParameterName) ||
                string.Equals(fallTriggerParameterName, recoverTriggerParameterName, StringComparison.Ordinal) ||
                string.Equals(fallTriggerParameterName, "Turn", StringComparison.Ordinal) ||
                string.Equals(recoverTriggerParameterName, "Turn", StringComparison.Ordinal))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(attackTriggerParameterName) ||
                (!string.Equals(fallTriggerParameterName, attackTriggerParameterName, StringComparison.Ordinal) &&
                 !string.Equals(recoverTriggerParameterName, attackTriggerParameterName, StringComparison.Ordinal));
        }

        private bool IsFallStateActiveOrEntering()
        {
            if (_animator.IsInTransition(0))
            {
                return _animator.GetNextAnimatorStateInfo(0).tagHash == FallStateTag;
            }

            return _animator.GetCurrentAnimatorStateInfo(0).tagHash == FallStateTag;
        }

        private bool IsAttackStateActiveOrEntering()
        {
            if (_animator.GetCurrentAnimatorStateInfo(0).tagHash == AttackStateTag)
            {
                return true;
            }

            return _animator.IsInTransition(0) &&
                _animator.GetNextAnimatorStateInfo(0).tagHash == AttackStateTag;
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
                FailConfiguration($"{nameof(PegaActorAnimationSetPresenter)} found more than one active Animator beside it in the composition mount.");
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
                FailConfiguration($"{nameof(PegaActorAnimationSetPresenter)} requires Animator Root Motion to be disabled; physical movement belongs to {nameof(PegaActorMovementMechanic)}.");
                return false;
            }

            candidate.runtimeAnimatorController = animationSet;
            if (!HasParameter(candidate, LocomotionParameter, AnimatorControllerParameterType.Float) ||
                !HasParameter(candidate, TurnAngleParameter, AnimatorControllerParameterType.Float) ||
                !HasParameter(candidate, TurnParameter, AnimatorControllerParameterType.Trigger) ||
                !HasParameter(candidate, TurnPlaybackSpeedParameter, AnimatorControllerParameterType.Float))
            {
                FailConfiguration($"{nameof(PegaActorAnimationSetPresenter)} requires Animator parameters 'Locomotion' (Float), 'TurnAngle' (Float), 'Turn' (Trigger) and 'TurnPlaybackSpeed' (Float).");
                return false;
            }

            _attackTriggerParameter = string.IsNullOrWhiteSpace(attackTriggerParameterName)
                ? 0
                : Animator.StringToHash(attackTriggerParameterName);
            _hasValidAttackTrigger = _attackMechanic == null ||
                (_attackTriggerParameter != 0 &&
                 _attackTriggerParameter != TurnParameter &&
                 HasParameter(candidate, _attackTriggerParameter, AnimatorControllerParameterType.Trigger));
            if (_attackMechanic != null && !_hasValidAttackTrigger)
            {
                _log.Error(
                    "Actor attack animation is not configured; locomotion presentation remains active.",
                    LogFields.Field("actorRoot", _signals.gameObject.name),
                    LogFields.Field("animationSet", animationSet.name),
                    LogFields.Field("attackTriggerParameterName", attackTriggerParameterName));
            }

            _fallTriggerParameter = string.IsNullOrWhiteSpace(fallTriggerParameterName)
                ? 0
                : Animator.StringToHash(fallTriggerParameterName);
            _recoverTriggerParameter = string.IsNullOrWhiteSpace(recoverTriggerParameterName)
                ? 0
                : Animator.StringToHash(recoverTriggerParameterName);
            _hasValidIncapacitationTriggers =
                _fallTriggerParameter != 0 &&
                _recoverTriggerParameter != 0 &&
                _fallTriggerParameter != _recoverTriggerParameter &&
                _fallTriggerParameter != TurnParameter &&
                _recoverTriggerParameter != TurnParameter &&
                (!_hasValidAttackTrigger ||
                 (_fallTriggerParameter != _attackTriggerParameter &&
                  _recoverTriggerParameter != _attackTriggerParameter)) &&
                HasParameter(candidate, _fallTriggerParameter, AnimatorControllerParameterType.Trigger) &&
                HasParameter(candidate, _recoverTriggerParameter, AnimatorControllerParameterType.Trigger);
            if (_incapacitationMechanic != null && !_hasValidIncapacitationTriggers)
            {
                _log.Error(
                    "Actor incapacitation animation triggers are not configured; the gameplay state and control restrictions remain active.",
                    LogFields.Field("actorRoot", _signals.gameObject.name),
                    LogFields.Field("animationSet", animationSet.name),
                    LogFields.Field("fallTriggerParameterName", fallTriggerParameterName),
                    LogFields.Field("recoverTriggerParameterName", recoverTriggerParameterName));
            }

            _animator = candidate;
            _hasPresentedIncapacitationState = false;
            _fallPresentationPending = false;
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
