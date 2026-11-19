using UnityEngine;

namespace PEGA.Actors.Runtime
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PEGA/Actors/Participation State")]
    public sealed class PegaActorParticipationState : MonoBehaviour
    {
        [System.NonSerialized]
        private bool _isCaptured;

        public bool IsCaptured => _isCaptured;

        public bool IsParticipating => isActiveAndEnabled && gameObject.activeInHierarchy && !_isCaptured;

        internal bool TryMarkCaptured()
        {
            if (_isCaptured || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return false;
            }

            _isCaptured = true;
            return true;
        }

    }
}
