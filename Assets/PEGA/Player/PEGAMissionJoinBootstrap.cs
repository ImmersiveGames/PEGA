using System.Collections;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.PlayerParticipation;
using UnityEngine;

namespace PEGA.Player
{
    [DisallowMultipleComponent]
    public sealed class PEGAMissionJoinBootstrap : ActivityContentBehaviour
    {
        private const string RequestSource = "PEGAMissionJoinBootstrap";
        private const string RequestReason = "pega.mission.initial-player-join";

        [SerializeField]
        private PlayerSessionJoinTrigger joinTrigger;

        private Coroutine _pendingRequest;
        private bool _requestClaimedForEntry;

        protected override void OnActivityContentEntered(ActivityContentLifecycleContext context)
        {
            if (_requestClaimedForEntry || context.Activity == null)
            {
                return;
            }

            _requestClaimedForEntry = true;
            _pendingRequest = StartCoroutine(RequestJoinAfterBindingRefresh());
        }

        protected override void OnActivityContentExited(ActivityContentLifecycleContext context)
        {
            if (_pendingRequest != null)
            {
                StopCoroutine(_pendingRequest);
                _pendingRequest = null;
            }

            _requestClaimedForEntry = false;
        }

        private IEnumerator RequestJoinAfterBindingRefresh()
        {
            // Wait one frame, then resume at its end after the binding module's Update pass.
            yield return null;
            yield return new WaitForEndOfFrame();
            _pendingRequest = null;

            if (!IsActivityContentActive)
            {
                yield break;
            }

            if (joinTrigger == null)
            {
                Debug.LogError("PEGA E04 cannot request the initial Player Join: PlayerSessionJoinTrigger is missing.", this);
                yield break;
            }

            var request = new LocalPlayerJoinRequest(RequestSource, RequestReason);
            LocalPlayerJoinOutcome outcome = joinTrigger.RequestJoin(request);
            if (outcome == null)
            {
                Debug.LogError("PEGA E04 Player Join returned no LocalPlayerJoinOutcome.", this);
                yield break;
            }

            bool admitted = outcome.Code == LocalPlayerJoinOutcomeCode.Joined &&
                            outcome.AdmissionState == LocalPlayerJoinAdmissionState.Admitted;
            string disposition = admitted ? "JOIN_ADMITTED" : "JOIN_NOT_ADMITTED";
            string message =
                $"PEGA E04 Mission Join: result='{disposition}' code='{outcome.Code}' admissionState='{outcome.AdmissionState}'.";

            if (admitted)
            {
                Debug.Log(message, this);
            }
            else
            {
                Debug.LogWarning(message, this);
            }
        }
    }
}
