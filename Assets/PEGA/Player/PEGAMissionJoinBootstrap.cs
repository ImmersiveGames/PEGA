using System.Collections;
using Immersive.Framework.ActivityFlow;
using Immersive.Framework.PlayerParticipation;
using Immersive.Logging.Loggers;
using Immersive.Logging.Records;
using PEGA.Diagnostics;
using UnityEngine;

namespace PEGA.Player
{
    [DisallowMultipleComponent]
    public sealed class PEGAMissionJoinBootstrap : ActivityContentBehaviour
    {
        private const string LogCategory = "PEGA.Gameplay.PlayerJoin";
        private const string RequestSource = "PEGAMissionJoinBootstrap";
        private const string RequestReason = "pega.mission.initial-player-join";

        [SerializeField]
        private PlayerSessionJoinTrigger joinTrigger;

        private Coroutine _pendingRequest;
        private bool _requestClaimedForEntry;

        private ScopedLogger _log;

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
                EnsureLoggerInitialized();
                _log.Error(
                    "PEGA E04 cannot request the initial Player Join: PlayerSessionJoinTrigger is missing.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestReason", RequestReason));
                yield break;
            }

            var request = new LocalPlayerJoinRequest(RequestSource, RequestReason);
            LocalPlayerJoinOutcome outcome = joinTrigger.RequestJoin(request);
            if (outcome == null)
            {
                EnsureLoggerInitialized();
                _log.Error(
                    "PEGA E04 Player Join returned no LocalPlayerJoinOutcome.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("requestReason", RequestReason));
                yield break;
            }

            bool admitted = outcome.Code == LocalPlayerJoinOutcomeCode.Joined &&
                            outcome.AdmissionState == LocalPlayerJoinAdmissionState.Admitted;
            EnsureLoggerInitialized();

            if (admitted)
            {
                _log.Debug(
                    "PEGA E04 Mission Join was admitted.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("code", outcome.Code),
                    LogFields.Field("admissionState", outcome.AdmissionState));
            }
            else
            {
                _log.Warning(
                    "PEGA E04 Mission Join was not admitted.",
                    LogFields.Field("actorRoot", gameObject.name),
                    LogFields.Field("code", outcome.Code),
                    LogFields.Field("admissionState", outcome.AdmissionState));
            }
        }

        private void EnsureLoggerInitialized()
        {
            _log ??= PEGALogging.CreateScopedLogger<PEGAMissionJoinBootstrap>(LogCategory, gameObject.name);
        }
    }
}
