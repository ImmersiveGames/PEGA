using Immersive.Framework.ActivityFlow;
using UnityEngine;

namespace PEGA.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ActivityReadinessParticipant))]
    public sealed class PegaDisputeReadinessProof : MonoBehaviour
    {
        [ContextMenu("Proof/Complete Dispute preparation")]
        private void CompleteDisputePreparation()
        {
            if (Application.isPlaying)
                GetComponent<ActivityReadinessParticipant>().CompletePreparation();
        }
    }
}
