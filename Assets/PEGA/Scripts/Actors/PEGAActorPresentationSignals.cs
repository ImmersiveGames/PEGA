using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Actors
{
    /// <summary>
    /// Publica, na raiz física do Actor, os sinais de apresentação derivados do gameplay.
    /// Não conhece Animator, clipes nem partes visuais.
    /// Destination: Production.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PEGAActorMovementMechanic))]
    public sealed class PEGAActorPresentationSignals : MonoBehaviour
    {
        private const string LogCategory = "PEGA.Presentation.Animation";

        [Header("Turn In Place")]
        [SerializeField, Min(0f)]
        [Tooltip("Velocidade planar máxima em que uma mudança de direção conta como giro no lugar (m/s).")]
        private float turnMaximumSpeedMetersPerSecond = 1f;

        [SerializeField, Range(1f, 180f)]
        [Tooltip("Diferença mínima entre a frente do Actor e a direção desejada para pedir um giro (°).")]
        private float turnMinimumAngleDegrees = 60f;

        [SerializeField, Range(0f, 179f)]
        [Tooltip("Diferença abaixo da qual um novo giro volta a ser permitido (°). Deve ser menor que o ângulo mínimo de giro.")]
        private float turnRearmAngleDegrees = 20f;

        private PEGAActorMovementMechanic _movementMechanic;
        private ScopedLogger _log;
        private bool _turnArmed = true;

        /// <summary>Velocidade planar atual do Actor (m/s).</summary>
        public float PlanarSpeedMetersPerSecond { get; private set; }

        /// <summary>Incrementa a cada novo pedido de giro no lugar.</summary>
        public int TurnRequestSequence { get; private set; }

        /// <summary>Ângulo do último pedido de giro (°): positivo à direita, negativo à esquerda.</summary>
        public float LastTurnAngleDegrees { get; private set; }

        /// <summary>Velocidade com que o gameplay gira o corpo do Actor (°/s).</summary>
        public float BodyTurnRateDegreesPerSecond =>
            _movementMechanic != null ? _movementMechanic.RotationSpeedDegreesPerSecond : 0f;

        private void Awake()
        {
            _log = PEGALogging.CreateScopedLogger<PEGAActorPresentationSignals>(
                LogCategory,
                gameObject.name);
            _movementMechanic = GetComponent<PEGAActorMovementMechanic>();

            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _log.Info(
                "Actor presentation signals configured.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("turnMaximumSpeedMps", turnMaximumSpeedMetersPerSecond),
                LogFields.Field("turnMinimumAngleDegrees", turnMinimumAngleDegrees),
                LogFields.Field("turnRearmAngleDegrees", turnRearmAngleDegrees));
        }

        private void LateUpdate()
        {
            if (!_movementMechanic.isActiveAndEnabled)
            {
                PlanarSpeedMetersPerSecond = 0f;
                _turnArmed = true;
                return;
            }

            Vector3 planarVelocity = _movementMechanic.CurrentPlanarVelocity;
            PlanarSpeedMetersPerSecond = new Vector2(planarVelocity.x, planarVelocity.z).magnitude;
            ResolveTurnRequest();
        }

        private void ResolveTurnRequest()
        {
            if (!_movementMechanic.HasCurrentMoveIntent)
            {
                _turnArmed = true;
                return;
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            float angle = Vector3.SignedAngle(forward, _movementMechanic.CurrentMoveDirection, Vector3.up);
            float absoluteAngle = Mathf.Abs(angle);

            if (absoluteAngle <= turnRearmAngleDegrees)
            {
                _turnArmed = true;
                return;
            }

            if (!_turnArmed ||
                absoluteAngle < turnMinimumAngleDegrees ||
                PlanarSpeedMetersPerSecond > turnMaximumSpeedMetersPerSecond)
            {
                return;
            }

            _turnArmed = false;
            LastTurnAngleDegrees = angle;
            TurnRequestSequence++;

            _log.Info(
                "Turn in place requested.",
                LogFields.Field("actorRoot", gameObject.name),
                LogFields.Field("turnAngleDegrees", angle),
                LogFields.Field("planarSpeedMps", PlanarSpeedMetersPerSecond),
                LogFields.Field("sequence", TurnRequestSequence));
        }

        private bool ValidateConfiguration()
        {
            if (_movementMechanic == null)
            {
                _log.Error(
                    $"{nameof(PEGAActorPresentationSignals)} requires {nameof(PEGAActorMovementMechanic)} on the same Actor root.",
                    LogFields.Field("actorRoot", gameObject.name));
                return false;
            }

            if (!IsFiniteNonNegative(turnMaximumSpeedMetersPerSecond) ||
                turnRearmAngleDegrees >= turnMinimumAngleDegrees)
            {
                _log.Error(
                    $"{nameof(PEGAActorPresentationSignals)} requires a non-negative turn speed limit and a rearm angle below the minimum turn angle.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("turnMaximumSpeedMps", turnMaximumSpeedMetersPerSecond),
                    LogFields.Field("turnMinimumAngleDegrees", turnMinimumAngleDegrees),
                    LogFields.Field("turnRearmAngleDegrees", turnRearmAngleDegrees));
                return false;
            }

            return true;
        }

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
