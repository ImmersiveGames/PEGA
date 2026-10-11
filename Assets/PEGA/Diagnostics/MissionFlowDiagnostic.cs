using UnityEngine;

namespace PEGA.Diagnostics
{
    /// <summary>
    /// Marcador temporário do FOUND-01. Registra mudanças diagnósticas de fase;
    /// não avança gameplay nem assume a navegação do Framework.
    /// </summary>
    public sealed class MissionFlowDiagnostic : MonoBehaviour
    {
        public enum Phase
        {
            Preparation,
            Assault,
            Escape
        }

        [SerializeField]
        private Phase phase = Phase.Preparation;

        private Phase _lastLoggedPhase;

        private void OnEnable()
        {
            _lastLoggedPhase = phase;
            Debug.Log($"FOUND-01 diagnostic: entered Mission Activity; phase={phase}.", this);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || _lastLoggedPhase == phase)
            {
                return;
            }

            _lastLoggedPhase = phase;
            Debug.Log($"FOUND-01 diagnostic phase changed to {phase}.", this);
        }
    }
}
